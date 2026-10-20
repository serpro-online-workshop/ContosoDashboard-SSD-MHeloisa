namespace ContosoDashboard.Models;

public class TaskDocumentAttachment
{
    public int TaskId { get; set; }
    public Guid DocumentId { get; set; }
    public int AttachedByUserId { get; set; }
    public DateTime AttachedAtUtc { get; set; } = DateTime.UtcNow;
    public TaskItem Task { get; set; } = null!;
    public Document Document { get; set; } = null!;
}
