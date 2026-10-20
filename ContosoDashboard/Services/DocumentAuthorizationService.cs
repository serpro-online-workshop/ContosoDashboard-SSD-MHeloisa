using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public interface IDocumentAuthorizationService
{
    Task<bool> CanViewDocumentAsync(Document document, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> CanUploadToProjectAsync(int projectId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> CanAccessAsync(Guid documentId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public sealed class DocumentAuthorizationService : IDocumentAuthorizationService
{
    private readonly ApplicationDbContext _db;

    public DocumentAuthorizationService(ApplicationDbContext db) => _db = db;

    public async Task<bool> CanViewDocumentAsync(
        Document document,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (document.StorageState != DocumentStorageState.Ready || principal.Identity?.IsAuthenticated != true)
            return false;

        var userId = GetUserId(principal);
        if (userId is null) return false;
        var caller = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId.Value)
            .Select(u => new { u.Role, u.Department })
            .SingleOrDefaultAsync(cancellationToken);
        if (caller is null) return false;
        if (caller.Role == UserRole.Administrator) return true;

        // Project removal revokes the original uploader first; a stale ownership/share record
        // cannot restore this user's access to their retained project document.
        if (document.ProjectId.HasValue && document.UploaderUserId == userId.Value)
        {
            var remainsInProject = await _db.Projects.AnyAsync(
                p => p.ProjectId == document.ProjectId &&
                     (p.ProjectManagerId == userId.Value ||
                      p.ProjectMembers.Any(m => m.UserId == userId.Value)),
                cancellationToken);
            if (!remainsInProject) return false;
        }

        if (document.UploaderUserId == userId.Value) return true;

        if (document.ProjectId.HasValue &&
            await _db.Projects.AnyAsync(
                p => p.ProjectId == document.ProjectId &&
                     (p.ProjectManagerId == userId.Value ||
                      p.ProjectMembers.Any(m => m.UserId == userId.Value)),
                cancellationToken))
            return true;

        return await _db.DocumentShares.AnyAsync(s =>
                s.DocumentId == document.DocumentId &&
                ((s.RecipientUserId == userId.Value) ||
                 (s.Department != null && caller.Department != null &&
                  s.Department == caller.Department)),
            cancellationToken);
    }

    public async Task<bool> CanUploadToProjectAsync(
        int projectId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true) return false;
        var userId = GetUserId(principal);
        if (userId is null) return false;

        var caller = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId.Value)
            .Select(u => u.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (caller == UserRole.Administrator) return true;

        return await _db.Projects.AnyAsync(
            p => p.ProjectId == projectId &&
                 (p.ProjectManagerId == userId.Value ||
                  p.ProjectMembers.Any(m => m.UserId == userId.Value)),
            cancellationToken);
    }

    public async Task<bool> CanAccessAsync(
        Guid documentId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
        return document is not null &&
               await CanViewDocumentAsync(document, principal, cancellationToken);
    }

    private static int? GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) && id > 0 ? id : null;
    }
}
