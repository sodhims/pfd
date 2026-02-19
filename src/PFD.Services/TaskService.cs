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

    public async Task<DailyTask> ToggleStartedAsync(int id, int userId)
    {
        var task = await _taskRepository.ToggleStartedAsync(id, userId);
        return task ?? throw new InvalidOperationException($"Task {id} not found");
    }

    public async Task<int> KickForwardInProgressTasksAsync(int userId)
    {
        return await _taskRepository.KickForwardInProgressTasksAsync(userId);
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

    public async Task<List<DailyTask>> GetUnscheduledTasksAsync(int userId)
    {
        return await _taskRepository.GetUnscheduledTasksAsync(userId);
    }

    public async Task<List<DailyTask>> SearchAllTasksAsync(int userId, int maxResults = 500)
    {
        return await _taskRepository.SearchAllTasksAsync(userId, maxResults);
    }

    // Task workflow transitions
    public async Task<int> ProcessDailyTaskTransitionsAsync(int userId)
    {
        return await _taskRepository.ProcessDailyTaskTransitionsAsync(userId);
    }

    public async Task<DailyTask?> MoveWaitingToTasksAsync(int taskId, int userId)
    {
        return await _taskRepository.MoveWaitingToTasksAsync(taskId, userId);
    }

    public async Task<List<DailyTask>> GetLongQueueTasksAsync(int userId, int daysThreshold = 2)
    {
        return await _taskRepository.GetLongQueueTasksAsync(userId, daysThreshold);
    }

    public async Task<int> CleanupIncompleteRecurringTasksAsync(int userId)
    {
        return await _taskRepository.CleanupIncompleteRecurringTasksAsync(userId);
    }

    public async Task<int> ReprocessTasksWithRelativeDatesAsync(int userId)
    {
        // Get all incomplete tasks for the user
        var allTasks = await _taskRepository.SearchAllTasksAsync(userId, 1000);
        var tasksToFix = allTasks.Where(t => !t.IsCompleted && HasRelativeDateInTitle(t.Title)).ToList();

        int fixedCount = 0;
        foreach (var task in tasksToFix)
        {
            var parseResult = TaskTimeParser.ParseWithRecurrence(task.Title);

            bool needsUpdate = false;

            // Fix title if it contains relative date words
            if (parseResult.CleanedTitle != task.Title)
            {
                task.Title = parseResult.CleanedTitle;
                needsUpdate = true;
            }

            // Fix date if parsed date is different
            if (parseResult.TaskDate.HasValue && parseResult.TaskDate.Value.Date != task.TaskDate.Date)
            {
                task.TaskDate = parseResult.TaskDate.Value;
                needsUpdate = true;
            }

            // Fix time if parsed time is different
            if (parseResult.ScheduledTime.HasValue && parseResult.ScheduledTime != task.ScheduledTime)
            {
                task.ScheduledTime = parseResult.ScheduledTime;
                task.IsAllDay = false;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                await _taskRepository.UpdateAsync(task);
                fixedCount++;
            }
        }

        return fixedCount;
    }

    private static bool HasRelativeDateInTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        return lower.Contains("tomorrow") ||
               lower.Contains("today") ||
               lower.Contains("tonight") ||
               lower.Contains("next week") ||
               lower.Contains("next month") ||
               lower.Contains("day after tomorrow") ||
               System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b(next|on|this)\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b");
    }

    // Participant management
    public async Task<List<Participant>> GetAllParticipantsAsync()
    {
        return await _taskRepository.GetAllParticipantsAsync();
    }

    public async Task<List<Participant>> GetRecentParticipantsAsync(int limit = 10)
    {
        return await _taskRepository.GetRecentParticipantsAsync(limit);
    }

    public async Task<Participant> CreateParticipantAsync(string name, string? email = null, string? phone = null)
    {
        return await _taskRepository.CreateParticipantAsync(name, email, phone);
    }

    public async Task AddParticipantToTaskAsync(int taskId, int participantId, int userId)
    {
        await _taskRepository.AddParticipantToTaskAsync(taskId, participantId, userId);
    }

    public async Task RemoveParticipantFromTaskAsync(int taskId, int participantId, int userId)
    {
        await _taskRepository.RemoveParticipantFromTaskAsync(taskId, participantId, userId);
    }

    public async Task<List<Participant>> GetTaskParticipantsAsync(int taskId)
    {
        return await _taskRepository.GetTaskParticipantsAsync(taskId);
    }
}
