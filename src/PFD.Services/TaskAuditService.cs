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
