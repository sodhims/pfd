using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data.Repositories;

public class TaskRepository
{
    private readonly IDbContextFactory<PfdDbContext> _contextFactory;

    public TaskRepository(IDbContextFactory<PfdDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Get the group IDs that a user belongs to (for including shared tasks in queries)
    /// </summary>
    private async Task<List<int>> GetUserGroupIdsAsync(PfdDbContext context, int userId)
    {
        return await context.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync();
    }

    public async Task<List<DailyTask>> GetTasksForDateAsync(DateTime date, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        // Fetch data first, then order client-side (SQLite doesn't support TimeSpan in ORDER BY)
        var tasks = await context.DailyTasks
            .Where(t => t.TaskDate.Date == date.Date &&
                        t.ParentTaskId == null &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .Include(t => t.Subtasks)
            .ToListAsync();

        return tasks
            .OrderBy(t => t.IsAllDay ? 1 : 0) // Scheduled tasks first
            .ThenBy(t => t.ScheduledTime ?? TimeSpan.MaxValue) // Then by time
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .ToList();
    }

    public async Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        // Fetch data first, then order client-side (SQLite doesn't support TimeSpan in ORDER BY)
        var tasks = await context.DailyTasks
            .Where(t => t.TaskDate.Date >= startDate.Date && t.TaskDate.Date <= endDate.Date &&
                        t.ParentTaskId == null &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .Include(t => t.Subtasks)
            .ToListAsync();

        return tasks
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.IsAllDay ? 1 : 0)
            .ThenBy(t => t.ScheduledTime ?? TimeSpan.MaxValue)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .ToList();
    }

    public async Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        return await context.DailyTasks
            .Where(t => t.TaskDate.Date < beforeDate.Date && !t.IsCompleted &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .ToListAsync();
    }

    public async Task<DailyTask?> GetByIdAsync(int id, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        return await context.DailyTasks
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
    }

    public async Task<DailyTask> CreateAsync(DailyTask task)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        context.DailyTasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    public async Task<DailyTask> UpdateAsync(DailyTask task)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        task.UpdatedAt = DateTime.UtcNow;
        context.DailyTasks.Update(task);
        await context.SaveChangesAsync();
        return task;
    }

    public async Task DeleteAsync(int id, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        var task = await context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
        if (task != null)
        {
            context.DailyTasks.Remove(task);
            await context.SaveChangesAsync();
        }
    }

    public async Task<DailyTask?> ToggleCompletionAsync(int id, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        var task = await context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
        if (task != null)
        {
            task.IsCompleted = !task.IsCompleted;
            task.CompletedAt = task.IsCompleted ? DateTime.UtcNow : null;

            // Track actual work end time and calculate total minutes for data mining
            if (task.IsCompleted)
            {
                task.ActualEndTime = DateTime.UtcNow;

                // If task was started, calculate final work session
                if (task.ActualStartTime.HasValue)
                {
                    var minutesWorked = (int)(DateTime.UtcNow - task.ActualStartTime.Value).TotalMinutes;
                    task.TotalMinutesWorked += minutesWorked;
                }
            }
            else
            {
                // Un-completing: clear end time but keep total minutes worked
                task.ActualEndTime = null;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
        return task;
    }

    public async Task<DailyTask?> ToggleStartedAsync(int id, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        var task = await context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
        if (task != null)
        {
            task.IsStarted = !task.IsStarted;
            task.StartedAt = task.IsStarted ? DateTime.UtcNow : null;

            // Track actual work start time for data mining
            if (task.IsStarted)
            {
                task.ActualStartTime = DateTime.UtcNow;
            }
            else if (task.ActualStartTime.HasValue)
            {
                // If un-starting, calculate time worked and add to total
                var minutesWorked = (int)(DateTime.UtcNow - task.ActualStartTime.Value).TotalMinutes;
                task.TotalMinutesWorked += minutesWorked;
                task.ActualStartTime = null;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
        return task;
    }

    public async Task<int> KickForwardInProgressTasksAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var today = DateTime.Today;

        // Find all tasks that are started but not completed, from past dates
        var inProgressTasks = await context.DailyTasks
            .Where(t => t.UserId == userId &&
                        t.IsStarted &&
                        !t.IsCompleted &&
                        t.TaskDate.Date < today)
            .ToListAsync();

        foreach (var task in inProgressTasks)
        {
            task.TaskDate = today;
            task.ScheduledTime = null; // Clear scheduled time, user can reschedule
            task.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return inProgressTasks.Count;
    }

    public async Task<List<DailyTask>> GetRecentTasksAsync(int userId, int days = 30)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);
        var startDate = DateTime.Today.AddDays(-days);

        return await context.DailyTasks
            .Where(t => t.TaskDate.Date >= startDate &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .OrderByDescending(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .ToListAsync();
    }

    public async Task<List<DailyTask>> GetUpcomingTasksAsync(int userId, int days = 14)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);
        var endDate = DateTime.Today.AddDays(days);

        return await context.DailyTasks
            .Where(t => t.TaskDate.Date >= DateTime.Today && t.TaskDate.Date <= endDate && !t.IsCompleted &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .ToListAsync();
    }

    /// <summary>
    /// Get all unscheduled tasks (tasks without a scheduled time, not completed, for today or future dates)
    /// </summary>
    public async Task<List<DailyTask>> GetUnscheduledTasksAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        return await context.DailyTasks
            .Where(t => t.TaskDate.Date >= DateTime.Today &&
                        !t.IsCompleted &&
                        !t.ScheduledTime.HasValue &&
                        t.ParentTaskId == null &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .ToListAsync();
    }

    // Participant management methods
    public async Task<List<Participant>> GetAllParticipantsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Participants
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Participant>> GetRecentParticipantsAsync(int limit = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Participants
            .OrderByDescending(p => p.LastMeetingDate ?? DateTime.MinValue)
            .ThenByDescending(p => p.MeetingCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Participant> CreateParticipantAsync(string name, string? email = null, string? phone = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Check if participant already exists
        var existing = await context.Participants
            .FirstOrDefaultAsync(p => p.Name == name || (email != null && p.Email == email));

        if (existing != null)
        {
            // Update phone if provided and not set
            if (!string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(existing.Phone))
            {
                existing.Phone = phone;
                await context.SaveChangesAsync();
            }
            return existing;
        }

        var participant = new Participant
        {
            Name = name,
            Email = email,
            Phone = phone,
            CreatedAt = DateTime.UtcNow
        };

        context.Participants.Add(participant);
        await context.SaveChangesAsync();
        return participant;
    }

    public async Task AddParticipantToTaskAsync(int taskId, int participantId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        var task = await context.DailyTasks
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));

        if (task == null) return;

        // Check if already added
        if (task.Participants.Any(tp => tp.ParticipantId == participantId))
            return;

        var taskParticipant = new TaskParticipant
        {
            TaskId = taskId,
            ParticipantId = participantId,
            IsSuggested = false
        };

        context.Set<TaskParticipant>().Add(taskParticipant);

        // Update participant's meeting count and last meeting date
        var participant = await context.Participants.FindAsync(participantId);
        if (participant != null)
        {
            participant.MeetingCount++;
            participant.LastMeetingDate = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task RemoveParticipantFromTaskAsync(int taskId, int participantId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        var task = await context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == taskId &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));

        if (task == null) return;

        var taskParticipant = await context.Set<TaskParticipant>()
            .FirstOrDefaultAsync(tp => tp.TaskId == taskId && tp.ParticipantId == participantId);

        if (taskParticipant != null)
        {
            context.Set<TaskParticipant>().Remove(taskParticipant);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Participant>> GetTaskParticipantsAsync(int taskId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<TaskParticipant>()
            .Where(tp => tp.TaskId == taskId)
            .Select(tp => tp.Participant)
            .ToListAsync();
    }

    /// <summary>
    /// Search all tasks for a user (for AI-powered search)
    /// </summary>
    public async Task<List<DailyTask>> SearchAllTasksAsync(int userId, int maxResults = 500)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var userGroupIds = await GetUserGroupIdsAsync(context, userId);

        return await context.DailyTasks
            .Where(t => t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value)))
            .OrderByDescending(t => t.TaskDate)
            .Take(maxResults)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .ToListAsync();
    }
}
