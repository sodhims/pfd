using PFD.Data.Repositories;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class TaskService : ITaskService
{
    private readonly TaskRepository _taskRepository;

    public TaskService(TaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<List<DailyTask>> GetTasksForDateAsync(DateTime date, int userId)
    {
        return await _taskRepository.GetTasksForDateAsync(date, userId);
    }

    public async Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate, int userId)
    {
        return await _taskRepository.GetTasksForDateRangeAsync(startDate, endDate, userId);
    }

    public async Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate, int userId)
    {
        return await _taskRepository.GetOverdueTasksAsync(beforeDate, userId);
    }

    public async Task<DailyTask?> GetTaskByIdAsync(int id, int userId)
    {
        return await _taskRepository.GetByIdAsync(id, userId);
    }

    public async Task<DailyTask> CreateTaskAsync(DailyTask task)
    {
        return await _taskRepository.CreateAsync(task);
    }

    public async Task<DailyTask> UpdateTaskAsync(DailyTask task)
    {
        return await _taskRepository.UpdateAsync(task);
    }

    public async Task DeleteTaskAsync(int id, int userId)
    {
        await _taskRepository.DeleteAsync(id, userId);
    }

    public async Task<DailyTask> ToggleCompletionAsync(int id, int userId)
    {
        var task = await _taskRepository.ToggleCompletionAsync(id, userId);
        return task ?? throw new InvalidOperationException($"Task {id} not found");
    }

    public async Task RescheduleTaskAsync(int taskId, DateTime newDate, int userId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, userId);
        if (task != null)
        {
            task.TaskDate = newDate;
            await _taskRepository.UpdateAsync(task);
        }
    }

    public async Task ScheduleTaskTimeAsync(int taskId, TimeSpan? time, int userId, int durationMinutes = 30)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, userId);
        if (task != null)
        {
            task.ScheduledTime = time;
            task.DurationMinutes = durationMinutes;
            task.IsAllDay = time == null;
            await _taskRepository.UpdateAsync(task);
        }
    }

    public async Task<List<DailyTask>> GetRecentTasksAsync(int userId, int days = 30)
    {
        return await _taskRepository.GetRecentTasksAsync(userId, days);
    }

    public async Task<List<DailyTask>> GetUpcomingTasksAsync(int userId, int days = 14)
    {
        return await _taskRepository.GetUpcomingTasksAsync(userId, days);
    }
}
