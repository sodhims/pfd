using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface ITaskAuditService
{
    /// <summary>
    /// Log a task operation
    /// </summary>
    Task LogAsync(
        int userId,
        string action,
        int? taskId = null,
        string? taskTitle = null,
        string? details = null,
        string? rawInput = null,
        string? source = null,
        bool success = true,
        string? errorMessage = null,
        DailyTask? beforeState = null,
        DailyTask? afterState = null);

    /// <summary>
    /// Get recent audit logs for a user
    /// </summary>
    Task<List<TaskAuditLog>> GetRecentLogsAsync(int userId, int count = 50);

    /// <summary>
    /// Get audit logs for a specific task
    /// </summary>
    Task<List<TaskAuditLog>> GetLogsForTaskAsync(int taskId);

    /// <summary>
    /// Get failed operations (potential lost tasks)
    /// </summary>
    Task<List<TaskAuditLog>> GetFailedOperationsAsync(int userId, int days = 7);

    /// <summary>
    /// Get create attempts without corresponding success
    /// </summary>
    Task<List<TaskAuditLog>> GetOrphanedCreateAttemptsAsync(int userId);

    /// <summary>
    /// Verify that all logged task creations actually exist in the database
    /// Returns list of audit logs where the task no longer exists
    /// </summary>
    Task<List<AuditVerificationResult>> VerifyTaskIntegrityAsync(int userId, int daysToCheck = 7);

    /// <summary>
    /// Verify a single task exists after creation and log the result
    /// Call this after CreateTaskAsync to confirm the task was persisted
    /// </summary>
    Task<bool> VerifyTaskCreatedAsync(int userId, int taskId, string taskTitle);
}

/// <summary>
/// Result of audit verification showing potential issues
/// </summary>
public class AuditVerificationResult
{
    public TaskAuditLog AuditLog { get; set; } = null!;
    public VerificationStatus Status { get; set; }
    public string Message { get; set; } = "";
}

public enum VerificationStatus
{
    Verified,           // Task exists and matches
    TaskMissing,        // Task was created but doesn't exist
    TaskDeleted,        // Task was explicitly deleted (OK)
    NoCreateFound,      // Create attempt without corresponding create
    Suspicious          // Other anomaly
}
