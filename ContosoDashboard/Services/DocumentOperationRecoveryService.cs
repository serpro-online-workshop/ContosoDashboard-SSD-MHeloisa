using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public interface IDocumentOperationRecoveryService
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}

public sealed class DocumentOperationRecoveryService : IDocumentOperationRecoveryService
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentFileStorage _storage;
    private readonly ILogger<DocumentOperationRecoveryService> _logger;

    public DocumentOperationRecoveryService(
        ApplicationDbContext db,
        IDocumentFileStorage storage,
        ILogger<DocumentOperationRecoveryService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _db.DocumentFileOperations
            .Where(o => o.State != DocumentFileOperationState.Completed &&
                        o.State != DocumentFileOperationState.Failed)
            .OrderBy(o => o.StartedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var operation in pending)
        {
            try
            {
                var current = operation.DocumentId.HasValue
                    ? await _db.Documents.AsNoTracking()
                        .SingleOrDefaultAsync(d => d.DocumentId == operation.DocumentId, cancellationToken)
                    : null;

                // A document committed just before a process interruption is current and must
                // never be removed by recovery.
                if (current is { StorageState: DocumentStorageState.Ready } &&
                    current.StorageKey == operation.CandidateStorageKey)
                {
                    if (operation.StagingKey is not null)
                        await _storage.DeleteStagedIfExistsAsync(operation.StagingKey, cancellationToken);
                    operation.State = DocumentFileOperationState.Completed;
                    operation.UpdatedAtUtc = DateTime.UtcNow;
                    continue;
                }

                if (operation.StagingKey is not null)
                    await _storage.DeleteStagedIfExistsAsync(operation.StagingKey, cancellationToken);

                if (!string.IsNullOrWhiteSpace(operation.CandidateStorageKey))
                {
                    var referenced = await _db.Documents.AnyAsync(
                        d => d.StorageKey == operation.CandidateStorageKey, cancellationToken);
                    if (!referenced)
                        await _storage.DeleteIfExistsAsync(operation.CandidateStorageKey, cancellationToken);
                }

                // Never delete PreviousStorageKey here. A failed replacement must preserve the old file.
                operation.State = DocumentFileOperationState.Failed;
                operation.FailureCode ??= "recovery-cleaned";
                operation.UpdatedAtUtc = DateTime.UtcNow;
            }
            catch (Exception exception)
            {
                operation.State = DocumentFileOperationState.CleanupPending;
                operation.FailureCode = "storage-failure";
                operation.UpdatedAtUtc = DateTime.UtcNow;
                _logger.LogError("Recovery for document operation {OperationId} failed ({FailureType}).",
                    operation.DocumentFileOperationId, exception.GetType().Name);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
