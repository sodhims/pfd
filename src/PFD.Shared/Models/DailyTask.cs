using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PFD.Shared.Enums;

namespace PFD.Shared.Models;

[Table("daily_tasks")]
public class DailyTask
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The user who owns this task
    /// </summary>
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime TaskDate { get; set; }

    public bool IsCompleted { get; set; } = false;

    public DateTime? CompletedAt { get; set; }

    public TaskType TaskType { get; set; } = TaskType.General;

    /// <summary>
    /// AI-generated metadata stored as JSON
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// For student-related tasks (thesis reviews, etc.)
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>
    /// Task deadline
    /// </summary>
    public DateTime? DueBy { get; set; }

    /// <summary>
    /// Scheduled time for the task (time of day only, date comes from TaskDate)
    /// </summary>
    public TimeSpan? ScheduledTime { get; set; }

    /// <summary>
    /// Duration in minutes (default 30 min)
    /// </summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>
    /// Whether this is an all-day task (no specific time)
    /// </summary>
    public bool IsAllDay { get; set; } = true;

    /// <summary>
    /// Display order for the day
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Parent task ID for subtasks (null if this is a top-level task)
    /// </summary>
    public int? ParentTaskId { get; set; }

    /// <summary>
    /// Navigation property to parent task
    /// </summary>
    [ForeignKey("ParentTaskId")]
    public virtual DailyTask? ParentTask { get; set; }

    /// <summary>
    /// Navigation property for subtasks
    /// </summary>
    public virtual ICollection<DailyTask> Subtasks { get; set; } = new List<DailyTask>();

    /// <summary>
    /// If non-null, this task is shared to the specified group
    /// </summary>
    public int? GroupId { get; set; }

    [ForeignKey("GroupId")]
    public virtual TaskGroup? Group { get; set; }

    /// <summary>
    /// Custom color for theming (hex code)
    /// </summary>
    [MaxLength(20)]
    public string? CustomColor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for meeting participants
    /// </summary>
    public virtual ICollection<TaskParticipant> Participants { get; set; } = new List<TaskParticipant>();
}
