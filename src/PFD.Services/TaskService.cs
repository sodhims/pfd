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

    public async Task<List<DailyTask>> GetTasksForDateAsync(DateTime date)
    {
        return await _taskRepository.GetTasksForDateAsync(date);
    }

    public async Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _taskRepository.GetTasksForDateRangeAsync(startDate, endDate);
    }

    public async Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate)
    {
        return await _taskRepository.GetOverdueTasksAsync(beforeDate);
    }

    public async Task<DailyTask?> GetTaskByIdAsync(int id)
    {
        return await _taskRepository.GetByIdAsync(id);
    }

    public async Task<DailyTask> CreateTaskAsync(DailyTask task)
    {
        return await _taskRepository.CreateAsync(task);
    }

    public async Task<DailyTask> UpdateTaskAsync(DailyTask task)
    {
        return await _taskRepository.UpdateAsync(task);
    }

    public async Task DeleteTaskAsync(int id)
    {
        await _taskRepository.DeleteAsync(id);
    }

    public async Task<DailyTask> ToggleCompletionAsync(int id)
    {
        var task = await _taskRepository.ToggleCompletionAsync(id);
        return task ?? throw new InvalidOperationException($"Task {id} not found");
    }

    public async Task RescheduleTaskAsync(int taskId, DateTime newDate)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.TaskDate = newDate;
            await _taskRepository.UpdateAsync(task);
        }
    }

    public async Task ScheduleTaskTimeAsync(int taskId, TimeSpan? time, int durationMinutes = 30)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.ScheduledTime = time;
            task.DurationMinutes = durationMinutes;
            task.IsAllDay = time == null;
            await _taskRepository.UpdateAsync(task);
        }
    }

    public async Task<List<DailyTask>> GetRecentTasksAsync(int days = 30)
    {
        return await _taskRepository.GetRecentTasksAsync(days);
    }

    public async Task<List<DailyTask>> GetUpcomingTasksAsync(int days = 14)
    {
        return await _taskRepository.GetUpcomingTasksAsync(days);
    }
}
