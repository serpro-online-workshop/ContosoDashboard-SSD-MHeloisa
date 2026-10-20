using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentTag
{
    public int DocumentTagId { get; set; }
    public Guid DocumentId { get; set; }

    [Required, StringLength(100)]
    public string DisplayValue { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string NormalizedValue { get; set; } = string.Empty;
    public Document Document { get; set; } = null!;
}
