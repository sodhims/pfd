using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class TaskAuditService : ITaskAuditService
{
    private readonly PfdDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public TaskAuditService(PfdDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task LogAsync(
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
        DailyTask? afterState = null)
    {
        try
        {
            var log = new TaskAuditLog
            {
                UserId = userId,
                TaskId = taskId,
                Action = action,
                TaskTitle = taskTitle,
                Details = details,
                RawInput = rawInput,
                Source = source,
                Success = success,
                ErrorMessage = errorMessage,
                BeforeStateJson = beforeState != null ? SerializeTaskState(beforeState) : null,
                AfterStateJson = afterState != null ? SerializeTaskState(afterState) : null,
                Timestamp = DateTime.UtcNow
            };

            _context.TaskAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Don't let audit logging break the main operation
            Console.WriteLine($"Audit log error: {ex.Message}");
        }
    }

    public async Task<List<TaskAuditLog>> GetRecentLogsAsync(int userId, int count = 50)
    {
        return await _context.TaskAuditLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<TaskAuditLog>> GetLogsForTaskAsync(int taskId)
    {
        return await _context.TaskAuditLogs
            .Where(l => l.TaskId == taskId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<TaskAuditLog>> GetFailedOperationsAsync(int userId, int days = 7)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await _context.TaskAuditLogs
            .Where(l => l.UserId == userId && !l.Success && l.Timestamp >= since)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<TaskAuditLog>> GetOrphanedCreateAttemptsAsync(int userId)
    {
        // Find CREATE_ATTEMPT logs that don't have a corresponding CREATE with the same RawInput
        var attempts = await _context.TaskAuditLogs
            .Where(l => l.UserId == userId && l.Action == AuditActions.CreateAttempt)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        var creates = await _context.TaskAuditLogs
            .Where(l => l.UserId == userId && l.Action == AuditActions.Create)
            .ToListAsync();

        var createInputs = creates.Select(c => c.RawInput).ToHashSet();

        return attempts.Where(a => !string.IsNullOrEmpty(a.RawInput) && !createInputs.Contains(a.RawInput)).ToList();
    }

    public async Task<List<AuditVerificationResult>> VerifyTaskIntegrityAsync(int userId, int daysToCheck = 7)
    {
        var results = new List<AuditVerificationResult>();
        var since = DateTime.UtcNow.AddDays(-daysToCheck);

        // Get all CREATE logs in the period
        var createLogs = await _context.TaskAuditLogs
            .Where(l => l.UserId == userId &&
                       l.Action == AuditActions.Create &&
                       l.Success &&
                       l.TaskId.HasValue &&
                       l.Timestamp >= since)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        // Get all DELETE logs to know which deletions were intentional
        var deleteLogs = await _context.TaskAuditLogs
            .Where(l => l.UserId == userId &&
                       l.Action == AuditActions.Delete &&
                       l.TaskId.HasValue &&
                       l.Timestamp >= since)
            .ToListAsync();

        var deletedTaskIds = deleteLogs.Select(d => d.TaskId!.Value).ToHashSet();

        // Get all current task IDs for this user
        var existingTaskIds = (await _context.DailyTasks
            .Where(t => t.UserId == userId)
            .Select(t => t.Id)
            .ToListAsync()).ToHashSet();

        // Check each create log
        foreach (var log in createLogs)
        {
            var taskId = log.TaskId!.Value;

            if (existingTaskIds.Contains(taskId))
            {
                // Task exists - verified OK
                results.Add(new AuditVerificationResult
                {
                    AuditLog = log,
                    Status = VerificationStatus.Verified,
                    Message = "Task exists in database"
                });
            }
            else if (deletedTaskIds.Contains(taskId))
            {
                // Task was deleted intentionally
                results.Add(new AuditVerificationResult
                {
                    AuditLog = log,
                    Status = VerificationStatus.TaskDeleted,
                    Message = "Task was deleted (intentional)"
                });
            }
            else
            {
                // Task is MISSING - this is a problem!
                results.Add(new AuditVerificationResult
                {
                    AuditLog = log,
                    Status = VerificationStatus.TaskMissing,
                    Message = $"ALERT: Task '{log.TaskTitle}' (ID: {taskId}) was created but does not exist!"
                });
            }
        }

        // Check for orphaned create attempts
        var orphanedAttempts = await GetOrphanedCreateAttemptsAsync(userId);
        foreach (var attempt in orphanedAttempts.Where(a => a.Timestamp >= since))
        {
            results.Add(new AuditVerificationResult
            {
                AuditLog = attempt,
                Status = VerificationStatus.NoCreateFound,
                Message = $"Create attempt for '{attempt.TaskTitle}' has no corresponding create record"
            });
        }

        return results;
    }

    public async Task<bool> VerifyTaskCreatedAsync(int userId, int taskId, string taskTitle)
    {
        // Small delay to ensure DB transaction completed
        await Task.Delay(100);

        var exists = await _context.DailyTasks
            .AnyAsync(t => t.Id == taskId && t.UserId == userId);

        if (!exists)
        {
            // Log the verification failure
            await LogAsync(
                userId,
                "VERIFY_FAILED",
                taskId: taskId,
                taskTitle: taskTitle,
                source: "VerifyTaskCreated",
                success: false,
                errorMessage: $"Task {taskId} was supposedly created but does not exist in database!");
        }

        return exists;
    }

    private string SerializeTaskState(DailyTask task)
    {
        // Serialize only relevant fields to avoid circular references
        var state = new
        {
            task.Id,
            task.Title,
            task.Description,
            task.TaskDate,
            task.ScheduledTime,
            task.DurationMinutes,
            task.IsAllDay,
            task.IsStarted,
            task.IsCompleted,
            task.DueBy,
            task.RecurrenceType,
            task.RecurrenceDays,
            task.ParentTaskId,
            task.DaysInQueue
        };
        return JsonSerializer.Serialize(state, _jsonOptions);
    }
}
