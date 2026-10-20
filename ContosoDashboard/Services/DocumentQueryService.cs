using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public enum DocumentLibraryScope { AllAccessible, MyDocuments, ProjectDocuments, SharedWithMe }

public sealed class DocumentLibraryQuery
{
    public string? Search { get; init; }
    public DocumentCategory? Category { get; init; }
    public int? ProjectId { get; init; }
    public DateTime? UploadedFrom { get; init; }
    public DateTime? UploadedThrough { get; init; }
    public string SortBy { get; init; } = "uploaded";
    public bool Descending { get; init; } = true;
    public DocumentLibraryScope Scope { get; init; } = DocumentLibraryScope.AllAccessible;
}

public interface IDocumentQueryService
{
    Task<List<Document>> GetAccessibleDocumentsAsync(
        ClaimsPrincipal principal,
        DocumentLibraryQuery? query = null,
        CancellationToken cancellationToken = default);
    Task<Document?> GetDocumentForUserAsync(
        Guid documentId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentQueryService : IDocumentQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentAuthorizationService _authorization;

    public DocumentQueryService(ApplicationDbContext db, IDocumentAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<List<Document>> GetAccessibleDocumentsAsync(
        ClaimsPrincipal principal,
        DocumentLibraryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new DocumentLibraryQuery();
        if (query.UploadedFrom.HasValue && query.UploadedThrough.HasValue &&
            query.UploadedFrom.Value.Date > query.UploadedThrough.Value.Date)
            return [];

        // Entitlement filtering is completed before searching, counting, suggesting, or sorting.
        var candidates = await _db.Documents.AsNoTracking()
            .Include(d => d.Project)
            .Include(d => d.Uploader)
            .Include(d => d.Tags)
            .Where(d => d.StorageState == DocumentStorageState.Ready)
            .ToListAsync(cancellationToken);
        var allowed = new List<Document>();
        foreach (var document in candidates)
        {
            if (await _authorization.CanViewDocumentAsync(document, principal, cancellationToken))
                allowed.Add(document);
        }

        var userId = GetUserId(principal);
        IEnumerable<Document> result = allowed;
        if (query.Scope == DocumentLibraryScope.MyDocuments)
            result = result.Where(d => d.UploaderUserId == userId);
        else if (query.Scope == DocumentLibraryScope.ProjectDocuments)
            result = result.Where(d => d.ProjectId.HasValue);
        else if (query.Scope == DocumentLibraryScope.SharedWithMe)
        {
            var dept = userId.HasValue
                ? await _db.Users.AsNoTracking().Where(u => u.UserId == userId.Value)
                    .Select(u => u.Department).SingleOrDefaultAsync(cancellationToken)
                : null;
            result = result.Where(d => d.UploaderUserId != userId &&
                _db.DocumentShares.Any(s => s.DocumentId == d.DocumentId &&
                    (s.RecipientUserId == userId ||
                     (dept != null && s.Department == dept))));
        }

        if (query.Category.HasValue) result = result.Where(d => d.Category == query.Category.Value);
        if (query.ProjectId.HasValue) result = result.Where(d => d.ProjectId == query.ProjectId.Value);
        if (query.UploadedFrom.HasValue) result = result.Where(d => d.UploadedAtUtc.Date >= query.UploadedFrom.Value.Date);
        if (query.UploadedThrough.HasValue) result = result.Where(d => d.UploadedAtUtc.Date <= query.UploadedThrough.Value.Date);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            result = result.Where(d =>
                d.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (d.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                d.OriginalFileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.Uploader.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (d.Project?.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                d.Tags.Any(tag => tag.DisplayValue.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        IOrderedEnumerable<Document> sorted = query.SortBy.ToLowerInvariant() switch
        {
            "title" => query.Descending
                ? result.OrderByDescending(d => d.Title, StringComparer.OrdinalIgnoreCase)
                : result.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase),
            "category" => query.Descending
                ? result.OrderByDescending(d => d.Category)
                : result.OrderBy(d => d.Category),
            "size" => query.Descending
                ? result.OrderByDescending(d => d.SizeBytes)
                : result.OrderBy(d => d.SizeBytes),
            _ => query.Descending
                ? result.OrderByDescending(d => d.UploadedAtUtc)
                : result.OrderBy(d => d.UploadedAtUtc)
        };
        return sorted.ThenBy(d => d.DocumentId).ToList();
    }

    public async Task<Document?> GetDocumentForUserAsync(
        Guid documentId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents.AsNoTracking()
            .Include(d => d.Project)
            .Include(d => d.Uploader)
            .Include(d => d.Tags)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
        if (document is null ||
            !await _authorization.CanViewDocumentAsync(document, principal, cancellationToken))
            return null;
        return document;
    }

    private static int? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }
}
