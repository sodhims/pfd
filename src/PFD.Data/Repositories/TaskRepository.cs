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

    public async Task<List<DailyTask>> GetTasksForDateAsync(DateTime date)
    {
        // Fetch data first, then order client-side (SQLite doesn't support TimeSpan in ORDER BY)
        var tasks = await _context.DailyTasks
            .Where(t => t.TaskDate.Date == date.Date)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .ToListAsync();

        return tasks
            .OrderBy(t => t.IsAllDay ? 1 : 0) // Scheduled tasks first
            .ThenBy(t => t.ScheduledTime ?? TimeSpan.MaxValue) // Then by time
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .ToList();
    }

    public async Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        // Fetch data first, then order client-side (SQLite doesn't support TimeSpan in ORDER BY)
        var tasks = await _context.DailyTasks
            .Where(t => t.TaskDate.Date >= startDate.Date && t.TaskDate.Date <= endDate.Date)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .ToListAsync();

        return tasks
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.IsAllDay ? 1 : 0)
            .ThenBy(t => t.ScheduledTime ?? TimeSpan.MaxValue)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .ToList();
    }

    public async Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate)
    {
        return await _context.DailyTasks
            .Where(t => t.TaskDate.Date < beforeDate.Date && !t.IsCompleted)
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .ToListAsync();
    }

    public async Task<DailyTask?> GetByIdAsync(int id)
    {
        return await _context.DailyTasks
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .FirstOrDefaultAsync(t => t.Id == id);
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

    public async Task DeleteAsync(int id)
    {
        var task = await _context.DailyTasks.FindAsync(id);
        if (task != null)
        {
            _context.DailyTasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<DailyTask?> ToggleCompletionAsync(int id)
    {
        var task = await _context.DailyTasks.FindAsync(id);
        if (task != null)
        {
            task.IsCompleted = !task.IsCompleted;
            task.CompletedAt = task.IsCompleted ? DateTime.UtcNow : null;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return task;
    }

    public async Task<List<DailyTask>> GetRecentTasksAsync(int days = 30)
    {
        var startDate = DateTime.Today.AddDays(-days);
        return await _context.DailyTasks
            .Where(t => t.TaskDate.Date >= startDate)
            .OrderByDescending(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .ToListAsync();
    }

    public async Task<List<DailyTask>> GetUpcomingTasksAsync(int days = 14)
    {
        var endDate = DateTime.Today.AddDays(days);
        return await _context.DailyTasks
            .Where(t => t.TaskDate.Date >= DateTime.Today && t.TaskDate.Date <= endDate && !t.IsCompleted)
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.SortOrder)
            .Include(t => t.Participants)
                .ThenInclude(tp => tp.Participant)
            .ToListAsync();
    }
}
