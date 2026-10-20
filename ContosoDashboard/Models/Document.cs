using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public enum DocumentCategory
{
    ProjectDocuments,
    TeamResources,
    PersonalFiles,
    Reports,
    Presentations,
    Other
}

public enum DocumentStorageState
{
    Ready,
    Replacing,
    Deleting
}

public class Document
{
    [Key]
    public Guid DocumentId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? DeclaredContentType { get; set; }

    [Required]
    [StringLength(255)]
    public string DetectedContentType { get; set; } = "application/octet-stream";

    [Required]
    [StringLength(50)]
    public string Extension { get; set; } = string.Empty;

    [Required]
    public long SizeBytes { get; set; }

    [Required]
    [StringLength(128)]
    public string StorageKey { get; set; } = string.Empty;

    [Required]
    public DocumentStorageState StorageState { get; set; } = DocumentStorageState.Ready;

    [Required]
    public int UploaderUserId { get; set; }

    public int? ProjectId { get; set; }

    [Required]
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public int Version { get; set; } = 1;

    [ForeignKey(nameof(UploaderUserId))]
    public virtual User Uploader { get; set; } = null!;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project? Project { get; set; }

    public virtual ICollection<DocumentTag> Tags { get; set; } = new HashSet<DocumentTag>();
    public virtual ICollection<DocumentShare> Shares { get; set; } = new HashSet<DocumentShare>();
    public virtual ICollection<TaskDocumentAttachment> TaskAttachments { get; set; } = new HashSet<TaskDocumentAttachment>();
}
