using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public sealed class UploadRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DocumentCategory Category { get; init; } = DocumentCategory.Other;
    public string? DeclaredContentType { get; init; }
    public int? ProjectId { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public Stream? Payload { get; init; }
    public string FileName { get; init; } = string.Empty;
    public IProgress<long>? Progress { get; init; }
}

public sealed record UploadResult(bool Success, Guid? DocumentId, string? ErrorCode, string Message);
public sealed record UploadProgress(Guid OperationId, long BytesWritten);

public interface IDocumentUploadService
{
    Task<IReadOnlyList<UploadResult>> UploadAsync(
        IReadOnlyList<UploadRequest> requests,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
    Task<UploadResult> UploadAsync(
        UploadRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentUploadService : IDocumentUploadService
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentFileStorage _storage;
    private readonly ILocalMalwareScanner _scanner;
    private readonly IDocumentFileValidator _validator;
    private readonly IDocumentAuthorizationService _authorization;
    private readonly DocumentOptions _options;
    private readonly ILogger<DocumentUploadService> _logger;

    public DocumentUploadService(
        ApplicationDbContext db,
        IDocumentFileStorage storage,
        ILocalMalwareScanner scanner,
        IDocumentFileValidator validator,
        IDocumentAuthorizationService authorization,
        IOptions<DocumentOptions> options,
        ILogger<DocumentUploadService> logger)
    {
        _db = db;
        _storage = storage;
        _scanner = scanner;
        _validator = validator;
        _authorization = authorization;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UploadResult>> UploadAsync(
        IReadOnlyList<UploadRequest> requests,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var results = new List<UploadResult>(requests.Count);
        foreach (var request in requests)
        {
            // A failed file does not cancel the remaining independent items.
            results.Add(await UploadAsync(request, principal, cancellationToken));
        }
        return results;
    }

    public async Task<UploadResult> UploadAsync(
        UploadRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = ReadUserId(principal);
        if (principal.Identity?.IsAuthenticated != true || userId is null)
            return Fail("authorization-denied", "Sign in again before uploading.");
        if (request.Payload is null)
            return Fail("empty-file", "Choose a file before uploading.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255 ||
            request.Description?.Length > 2000 || !Enum.IsDefined(request.Category))
            return Fail("invalid-metadata", "Enter a title up to 255 characters, a valid category, and a description up to 2,000 characters.");
        if (request.Tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Trim().Length > 100))
            return Fail("invalid-metadata", "Each tag must contain 1–100 characters.");

        var callerExists = await _db.Users.AnyAsync(u => u.UserId == userId.Value, cancellationToken);
        if (!callerExists) return Fail("authorization-denied", "Your training account is not available.");
        if (request.ProjectId.HasValue &&
            !await _authorization.CanUploadToProjectAsync(request.ProjectId.Value, principal, cancellationToken))
            return Fail("authorization-denied", "You cannot upload to that project.");

        var operationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var stagingKey = _storage.NewStagingKey();
        var storageKey = _storage.NewStorageKey();
        var operation = new DocumentFileOperation
        {
            DocumentFileOperationId = operationId,
            DocumentId = documentId,
            Kind = DocumentFileOperationKind.Upload,
            State = DocumentFileOperationState.Staging,
            CandidateStorageKey = storageKey,
            StagingKey = stagingKey,
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            _db.DocumentFileOperations.Add(operation);
            await _db.SaveChangesAsync(cancellationToken);

            var actualSize = await _storage.SaveStagedAsync(
                request.Payload, request.FileName, stagingKey, _options.MaxFileSizeBytes,
                request.Progress, cancellationToken);
            if (actualSize == 0)
                return await FailOperationAsync(operation, stagingKey, storageKey, "empty-file", "The selected file is empty.", cancellationToken);

            DocumentValidationResult validation;
            await using (var staged = new FileStream(_storage.GetStagedFilePath(stagingKey), FileMode.Open, FileAccess.Read, FileShare.Read))
                validation = _validator.Validate(staged, request.FileName, request.DeclaredContentType, _options.MaxFileSizeBytes);

            if (!validation.IsValid)
                return await FailOperationAsync(operation, stagingKey, storageKey,
                    validation.ErrorCode ?? "unsupported-type",
                    validation.ErrorMessage ?? "The file could not be validated.", cancellationToken);

            operation.State = DocumentFileOperationState.Scanned;
            operation.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var scan = await _scanner.ScanAsync(_storage.GetStagedFilePath(stagingKey), cancellationToken);
            if (scan.WasDetected)
                return await FailOperationAsync(operation, stagingKey, storageKey, "malware-detected",
                    "The local malware scanner detected unsafe content. Choose a different file.", cancellationToken);
            if (scan.IsFailure || !scan.IsClean)
                return await FailOperationAsync(operation, stagingKey, storageKey,
                    scan.FailureCode is "scanner-unavailable" ? "scanner-unavailable" : "scanner-error",
                    "The required local malware check could not complete. Retry after the scanner is available.", cancellationToken);

            await _storage.FinalizeToStorageAsync(stagingKey, storageKey, cancellationToken);
            operation.State = DocumentFileOperationState.CandidatePublished;
            operation.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var document = new Document
            {
                DocumentId = documentId,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Category = request.Category,
                OriginalFileName = request.FileName.Trim(),
                DeclaredContentType = request.DeclaredContentType is { Length: > 255 } ? request.DeclaredContentType[..255] : request.DeclaredContentType,
                DetectedContentType = validation.DetectedContentType!,
                Extension = validation.NormalizedExtension!,
                SizeBytes = actualSize,
                StorageKey = storageKey,
                StorageState = DocumentStorageState.Ready,
                UploaderUserId = userId.Value,
                ProjectId = request.ProjectId,
                UploadedAtUtc = now,
                UpdatedAtUtc = now,
                Version = 1
            };
            foreach (var tag in request.Tags.Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                document.Tags.Add(new DocumentTag { DisplayValue = tag, NormalizedValue = tag.ToUpperInvariant() });

            _db.Documents.Add(document);
            _db.DocumentActivities.Add(new DocumentActivity
            {
                DocumentReferenceId = documentId,
                ActorUserId = userId.Value,
                Action = DocumentActivityAction.Upload,
                OccurredAtUtc = now,
                FileTypeAtEvent = document.DetectedContentType,
                UploaderUserIdAtEvent = userId.Value
            });
            operation.State = DocumentFileOperationState.Completed;
            operation.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new UploadResult(true, document.DocumentId, null, "Upload succeeded.");
        }
        catch (Exception exception)
        {
            var code = GetFailureCode(exception);
            _logger.LogWarning("Document upload operation {OperationId} failed with {FailureCode}.", operationId, code);
            await CleanupFailedOperationAsync(operation, stagingKey, storageKey, code);
            var message = code switch
            {
                "storage-full" => "Storage is full. Free local space and retry this file; other completed files are unchanged.",
                "size-exceeded" => "The file exceeds the 25 MiB limit.",
                "scanner-unavailable" => "The required local malware scanner is unavailable. Retry after it is restored.",
                _ => "The file could not be saved safely. Retry this file; completed files are unchanged."
            };
            return Fail(code, message);
        }
    }

    private async Task<UploadResult> FailOperationAsync(
        DocumentFileOperation operation,
        string stagingKey,
        string storageKey,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        await _storage.DeleteStagedIfExistsAsync(stagingKey, CancellationToken.None);
        await _storage.DeleteIfExistsAsync(storageKey, CancellationToken.None);
        operation.State = DocumentFileOperationState.Failed;
        operation.FailureCode = code;
        operation.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Fail(code, message);
    }

    private async Task CleanupFailedOperationAsync(
        DocumentFileOperation operation,
        string stagingKey,
        string storageKey,
        string failureCode)
    {
        try
        {
            await _storage.DeleteStagedIfExistsAsync(stagingKey, CancellationToken.None);
            await _storage.DeleteIfExistsAsync(storageKey, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            _logger.LogError("Cleanup for document operation {OperationId} failed ({FailureType}).",
                operation.DocumentFileOperationId, cleanupException.GetType().Name);
            operation.State = DocumentFileOperationState.CleanupPending;
            operation.FailureCode = "storage-failure";
        }

        try
        {
            _db.ChangeTracker.Clear();
            var persisted = await _db.DocumentFileOperations
                .SingleOrDefaultAsync(o => o.DocumentFileOperationId == operation.DocumentFileOperationId);
            if (persisted is not null)
            {
                if (persisted.State != DocumentFileOperationState.CleanupPending)
                    persisted.State = DocumentFileOperationState.Failed;
                persisted.FailureCode ??= failureCode;
                persisted.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception journalException)
        {
            _logger.LogError("Document operation journal update failed ({FailureType}).", journalException.GetType().Name);
        }
    }

    private static string GetFailureCode(Exception exception)
    {
        if (exception is DocumentSizeLimitException) return "size-exceeded";
        if (exception is OperationCanceledException) return "storage-failure";
        if (exception is IOException io)
        {
            var nativeError = io.HResult & 0xffff;
            if (nativeError is 28 or 112) return "storage-full";
        }
        return "storage-failure";
    }

    private static int? ReadUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }

    private static UploadResult Fail(string code, string message) => new(false, null, code, message);
}
