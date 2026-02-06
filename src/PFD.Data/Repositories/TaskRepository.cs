using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data.Repositories;

public class TaskRepository
{
    private readonly PfdDbContext _context;

    public TaskRepository(PfdDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get the group IDs that a user belongs to (for including shared tasks in queries)
    /// </summary>
    private async Task<List<int>> GetUserGroupIdsAsync(int userId)
    {
        return await _context.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync();
    }

    public async Task<List<DailyTask>> GetTasksForDateAsync(DateTime date, int userId)
    {
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        // Fetch data first, then order client-side (SQLite doesn't support TimeSpan in ORDER BY)
        var tasks = await _context.DailyTasks
            .Where(t => t.TaskDate.Date == date.Date &&
                        t.ParentTaskId == null &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
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
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        // Fetch data first, then order client-side (SQLite doesn't support TimeSpan in ORDER BY)
        var tasks = await _context.DailyTasks
            .Where(t => t.TaskDate.Date >= startDate.Date && t.TaskDate.Date <= endDate.Date &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
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
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        return await _context.DailyTasks
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
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        return await _context.DailyTasks
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
    }

    public async Task<DailyTask> CreateAsync(DailyTask task)
    {
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        _context.DailyTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<DailyTask> UpdateAsync(DailyTask task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        _context.DailyTasks.Update(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task DeleteAsync(int id, int userId)
    {
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        var task = await _context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
        if (task != null)
        {
            _context.DailyTasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<DailyTask?> ToggleCompletionAsync(int id, int userId)
    {
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        var task = await _context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == id &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));
        if (task != null)
        {
            task.IsCompleted = !task.IsCompleted;
            task.CompletedAt = task.IsCompleted ? DateTime.UtcNow : null;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return task;
    }

    public async Task<List<DailyTask>> GetRecentTasksAsync(int userId, int days = 30)
    {
        var userGroupIds = await GetUserGroupIdsAsync(userId);
        var startDate = DateTime.Today.AddDays(-days);

        return await _context.DailyTasks
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
        var userGroupIds = await GetUserGroupIdsAsync(userId);
        var endDate = DateTime.Today.AddDays(days);

        return await _context.DailyTasks
            .Where(t => t.TaskDate.Date >= DateTime.Today && t.TaskDate.Date <= endDate && !t.IsCompleted &&
                        (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))))
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .Include(t => t.Group)
            .ToListAsync();
    }

    // Participant management methods
    public async Task<List<Participant>> GetAllParticipantsAsync()
    {
        return await _context.Participants
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Participant>> GetRecentParticipantsAsync(int limit = 10)
    {
        return await _context.Participants
            .OrderByDescending(p => p.LastMeetingDate ?? DateTime.MinValue)
            .ThenByDescending(p => p.MeetingCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Participant> CreateParticipantAsync(string name, string? email = null, string? phone = null)
    {
        // Check if participant already exists
        var existing = await _context.Participants
            .FirstOrDefaultAsync(p => p.Name == name || (email != null && p.Email == email));

        if (existing != null)
        {
            // Update phone if provided and not set
            if (!string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(existing.Phone))
            {
                existing.Phone = phone;
                await _context.SaveChangesAsync();
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

        _context.Participants.Add(participant);
        await _context.SaveChangesAsync();
        return participant;
    }

    public async Task AddParticipantToTaskAsync(int taskId, int participantId, int userId)
    {
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        var task = await _context.DailyTasks
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

        _context.Set<TaskParticipant>().Add(taskParticipant);

        // Update participant's meeting count and last meeting date
        var participant = await _context.Participants.FindAsync(participantId);
        if (participant != null)
        {
            participant.MeetingCount++;
            participant.LastMeetingDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task RemoveParticipantFromTaskAsync(int taskId, int participantId, int userId)
    {
        var userGroupIds = await GetUserGroupIdsAsync(userId);

        var task = await _context.DailyTasks
            .FirstOrDefaultAsync(t => t.Id == taskId &&
                (t.UserId == userId || (t.GroupId != null && userGroupIds.Contains(t.GroupId.Value))));

        if (task == null) return;

        var taskParticipant = await _context.Set<TaskParticipant>()
            .FirstOrDefaultAsync(tp => tp.TaskId == taskId && tp.ParticipantId == participantId);

        if (taskParticipant != null)
        {
            _context.Set<TaskParticipant>().Remove(taskParticipant);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Participant>> GetTaskParticipantsAsync(int taskId)
    {
        return await _context.Set<TaskParticipant>()
            .Where(tp => tp.TaskId == taskId)
            .Select(tp => tp.Participant)
            .ToListAsync();
    }
}
