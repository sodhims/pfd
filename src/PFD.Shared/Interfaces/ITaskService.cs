using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface ITaskService
{
    Task<List<DailyTask>> GetTasksForDateAsync(DateTime date);
    Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate);
    Task<DailyTask?> GetTaskByIdAsync(int id);
    Task<DailyTask> CreateTaskAsync(DailyTask task);
    Task<DailyTask> UpdateTaskAsync(DailyTask task);
    Task DeleteTaskAsync(int id);
    Task<DailyTask> ToggleCompletionAsync(int id);
    Task RescheduleTaskAsync(int taskId, DateTime newDate);
    Task ScheduleTaskTimeAsync(int taskId, TimeSpan? time, int durationMinutes = 30);
    Task<List<DailyTask>> GetRecentTasksAsync(int days = 30);
    Task<List<DailyTask>> GetUpcomingTasksAsync(int days = 14);
}
