using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface ITaskService
{
    Task<List<DailyTask>> GetTasksForDateAsync(DateTime date, int userId);
    Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate, int userId);
    Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate, int userId);
    Task<DailyTask?> GetTaskByIdAsync(int id, int userId);
    Task<DailyTask> CreateTaskAsync(DailyTask task);
    Task<DailyTask> UpdateTaskAsync(DailyTask task);
    Task DeleteTaskAsync(int id, int userId);
    Task<DailyTask> ToggleCompletionAsync(int id, int userId);
    Task RescheduleTaskAsync(int taskId, DateTime newDate, int userId);
    Task ScheduleTaskTimeAsync(int taskId, TimeSpan? time, int userId, int durationMinutes = 30);
    Task<List<DailyTask>> GetRecentTasksAsync(int userId, int days = 30);
    Task<List<DailyTask>> GetUpcomingTasksAsync(int userId, int days = 14);
}
