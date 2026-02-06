using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("prompt_templates")]
public class PromptTemplate
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public PromptCategory Category { get; set; }

    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Is this a built-in template or user-created?
    /// </summary>
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// User who created this template (null for built-in)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Is this the active template for its category?
    /// </summary>
    public bool IsActive { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PromptCategory
{
    /// <summary>
    /// For productivity insights and patterns
    /// </summary>
    Insights,

    /// <summary>
    /// For task prioritization
    /// </summary>
    Prioritization,

    /// <summary>
    /// For scheduling suggestions
    /// </summary>
    Scheduling,

    /// <summary>
    /// For calendar pattern analysis
    /// </summary>
    CalendarAnalysis,

    /// <summary>
    /// For task metadata augmentation
    /// </summary>
    TaskAugmentation
}
