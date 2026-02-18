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
}
