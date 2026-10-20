using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentActivity
{
    public int DocumentActivityId { get; set; }
    public Guid DocumentReferenceId { get; set; }
    public int ActorUserId { get; set; }
    public DocumentActivityAction Action { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(255)]
    public string? FileTypeAtEvent { get; set; }
    public int? UploaderUserIdAtEvent { get; set; }
}

public enum DocumentActivityAction
{
    Upload,
    Replacement,
    MetadataEdit,
    Download,
    Preview,
    Deletion,
    ShareAdded,
    ShareRemoved
}
