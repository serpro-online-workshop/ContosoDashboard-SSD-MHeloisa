using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentFileOperation
{
    public Guid DocumentFileOperationId { get; set; } = Guid.NewGuid();
    public Guid? DocumentId { get; set; }
    public DocumentFileOperationKind Kind { get; set; }
    public DocumentFileOperationState State { get; set; }

    [StringLength(128)]
    public string? PreviousStorageKey { get; set; }

    [StringLength(128)]
    public string? CandidateStorageKey { get; set; }

    [StringLength(256)]
    public string? StagingKey { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(64)]
    public string? FailureCode { get; set; }
}

public enum DocumentFileOperationKind { Upload, Replacement, Deletion, Cleanup }
public enum DocumentFileOperationState { Staging, Scanned, CandidatePublished, MetadataSwitched, CleanupPending, Completed, Failed }
