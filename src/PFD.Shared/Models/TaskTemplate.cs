using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PFD.Shared.Enums;

namespace PFD.Shared.Models;

/// <summary>
/// A reusable task template with optional subtask templates.
/// Users can create templates for recurring task patterns.
/// </summary>
[Table("task_templates")]
public class TaskTemplate
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The user who owns this template
    /// </summary>
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    /// <summary>
    /// Name of the template (e.g., "Review Video", "Create Video for Class")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this template is for
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Default task type for the parent task
    /// </summary>
    public TaskType DefaultTaskType { get; set; } = TaskType.General;

    /// <summary>
    /// Default duration in minutes for the parent task
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to schedule this as an all-day task by default
    /// </summary>
    public bool DefaultIsAllDay { get; set; } = true;

    /// <summary>
    /// Optional custom color for tasks created from this template
    /// </summary>
    [MaxLength(20)]
    public string? DefaultColor { get; set; }

    /// <summary>
    /// Sort order for display in template list
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this template is active/visible
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for subtask templates
    /// </summary>
    public virtual ICollection<SubtaskTemplate> SubtaskTemplates { get; set; } = new List<SubtaskTemplate>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A subtask template that belongs to a TaskTemplate.
/// </summary>
[Table("subtask_templates")]
public class SubtaskTemplate
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The parent task template
    /// </summary>
    public int TaskTemplateId { get; set; }

    [ForeignKey("TaskTemplateId")]
    public virtual TaskTemplate? TaskTemplate { get; set; }

    /// <summary>
    /// Title of the subtask (e.g., "Run video", "Create visualization")
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the subtask
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for the subtask within the template
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Default duration in minutes (optional)
    /// </summary>
    public int? DurationMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
