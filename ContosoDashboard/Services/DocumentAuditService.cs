using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentAuditService
{
    Task RecordAsync(Guid documentId, int actorUserId, DocumentActivityAction action, string? contentType = null, int? uploaderUserIdAtEvent = null);
}

public class DocumentAuditService : IDocumentAuditService
{
    private readonly ApplicationDbContext _db;

    public DocumentAuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(Guid documentId, int actorUserId, DocumentActivityAction action, string? contentType = null, int? uploaderUserIdAtEvent = null)
    {
        _db.DocumentActivities.Add(new DocumentActivity
        {
            DocumentReferenceId = documentId,
            ActorUserId = actorUserId,
            Action = action,
            FileTypeAtEvent = contentType,
            UploaderUserIdAtEvent = uploaderUserIdAtEvent,
            OccurredAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
