using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    public int DocumentShareId { get; set; }
    public Guid DocumentId { get; set; }
    public int? RecipientUserId { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    public int GrantedByUserId { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public Document Document { get; set; } = null!;
    public User? RecipientUser { get; set; }
    public User GrantedByUser { get; set; } = null!;
}
