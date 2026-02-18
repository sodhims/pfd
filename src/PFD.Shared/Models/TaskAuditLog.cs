using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

/// <summary>
/// Audit log for tracking all task interactions - ensures no task is ever silently dropped
/// </summary>
[Table("task_audit_logs")]
public class TaskAuditLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The user who performed the action
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The task ID (if applicable, null for failed create attempts)
    /// </summary>
    public int? TaskId { get; set; }

    /// <summary>
    /// Type of action performed
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Task title at the time of action
    /// </summary>
    [MaxLength(500)]
    public string? TaskTitle { get; set; }

    /// <summary>
    /// Detailed description of what happened
    /// </summary>
    [MaxLength(2000)]
    public string? Details { get; set; }

    /// <summary>
    /// The raw input from user (for create/update operations)
    /// </summary>
    [MaxLength(1000)]
    public string? RawInput { get; set; }

    /// <summary>
    /// Where the action originated (AddTask, SimilarTasksModal, Merge, Import, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? Source { get; set; }

    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if failed
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JSON snapshot of task state before change (for updates/deletes)
    /// </summary>
    public string? BeforeStateJson { get; set; }

    /// <summary>
    /// JSON snapshot of task state after change
    /// </summary>
    public string? AfterStateJson { get; set; }

    /// <summary>
    /// Timestamp of the action
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Common audit action types
/// </summary>
public static class AuditActions
{
    public const string Create = "CREATE";
    public const string CreateAttempt = "CREATE_ATTEMPT";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Complete = "COMPLETE";
    public const string Uncomplete = "UNCOMPLETE";
    public const string Start = "START";
    public const string Move = "MOVE";
    public const string Merge = "MERGE";
    public const string AddSubtask = "ADD_SUBTASK";
    public const string Replace = "REPLACE";
    public const string Schedule = "SCHEDULE";
    public const string Unschedule = "UNSCHEDULE";
    public const string ModalCancel = "MODAL_CANCEL";
    public const string ModalConfirm = "MODAL_CONFIRM";
    public const string Recover = "RECOVER";
}
