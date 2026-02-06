using System.Net.Http.Json;
using System.Text.Json;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Mobile.Services;

public class ApiService : ITaskService, IAuthService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    // ==================== AUTH ====================

    public async Task<User?> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/login",
                new { username, password });

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
            return result == null ? null : MapUser(result);
        }
        catch
        {
            return null;
        }
    }

    public async Task<User?> RegisterAsync(string username, string password, string? displayName = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/register",
                new { username, password, displayName });

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
            return result == null ? null : MapUser(result);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        // We'll check this during register on the server side
        return false;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<UserResponse>($"{_baseUrl}/api/auth/user/{userId}", JsonOptions);
            return result == null ? null : MapUser(result);
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateUserSettingsAsync(int userId, string theme, bool isDailyView, bool useLargeText)
    {
        try
        {
            await _http.PutAsJsonAsync($"{_baseUrl}/api/auth/settings/{userId}",
                new { theme, isDailyView, useLargeText });
        }
        catch { }
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        // Handled server-side during login
    }

    // Admin methods - not implemented in mobile app
    public Task<List<User>> GetAllUsersAsync() => Task.FromResult(new List<User>());
    public Task<bool> ResetPasswordAsync(int userId, string newPassword) => Task.FromResult(false);
    public Task<bool> DeleteUserAsync(int userId) => Task.FromResult(false);
    public Task<bool> IsAdminAsync(int userId) => Task.FromResult(false);
    public Task<AdminStats> GetAdminStatsAsync() => Task.FromResult(new AdminStats());

    // ==================== TASKS ====================

    public async Task<List<DailyTask>> GetTasksForDateAsync(DateTime date, int userId)
    {
        try
        {
            var tasks = await _http.GetFromJsonAsync<List<TaskResponse>>(
                $"{_baseUrl}/api/tasks/{userId}/date/{date:yyyy-MM-dd}", JsonOptions);
            return tasks?.Select(MapTask).ToList() ?? new List<DailyTask>();
        }
        catch
        {
            return new List<DailyTask>();
        }
    }

    public async Task<List<DailyTask>> GetOverdueTasksAsync(DateTime beforeDate, int userId)
    {
        try
        {
            var tasks = await _http.GetFromJsonAsync<List<TaskResponse>>(
                $"{_baseUrl}/api/tasks/{userId}/overdue/{beforeDate:yyyy-MM-dd}", JsonOptions);
            return tasks?.Select(MapTask).ToList() ?? new List<DailyTask>();
        }
        catch
        {
            return new List<DailyTask>();
        }
    }

    public async Task<List<DailyTask>> GetTasksForDateRangeAsync(DateTime startDate, DateTime endDate, int userId)
    {
        try
        {
            var tasks = await _http.GetFromJsonAsync<List<TaskResponse>>(
                $"{_baseUrl}/api/tasks/{userId}/range?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", JsonOptions);
            return tasks?.Select(MapTask).ToList() ?? new List<DailyTask>();
        }
        catch
        {
            return new List<DailyTask>();
        }
    }

    public async Task<DailyTask> CreateTaskAsync(DailyTask task)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/tasks", new
            {
                title = task.Title,
                taskDate = task.TaskDate,
                sortOrder = task.SortOrder,
                isAllDay = task.IsAllDay,
                scheduledTime = task.ScheduledTime,
                durationMinutes = task.DurationMinutes,
                userId = task.UserId
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
                return result != null ? MapTask(result) : task;
            }
        }
        catch { }
        return task;
    }

    public async Task<DailyTask> UpdateTaskAsync(DailyTask task)
    {
        try
        {
            await _http.PutAsJsonAsync($"{_baseUrl}/api/tasks/{task.Id}", new
            {
                title = task.Title,
                taskDate = task.TaskDate,
                isCompleted = task.IsCompleted,
                sortOrder = task.SortOrder,
                isAllDay = task.IsAllDay,
                scheduledTime = task.ScheduledTime,
                durationMinutes = task.DurationMinutes,
                userId = task.UserId
            });
        }
        catch { }
        return task;
    }

    public async Task<DailyTask?> GetTaskByIdAsync(int id, int userId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<TaskResponse>($"{_baseUrl}/api/tasks/{id}/{userId}", JsonOptions);
            return result != null ? MapTask(result) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<DailyTask> ToggleCompletionAsync(int taskId, int userId)
    {
        try
        {
            var response = await _http.PostAsync($"{_baseUrl}/api/tasks/{taskId}/toggle/{userId}", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
                if (result != null) return MapTask(result);
            }
        }
        catch { }
        return new DailyTask { Id = taskId };
    }

    public async Task DeleteTaskAsync(int taskId, int userId)
    {
        try
        {
            await _http.DeleteAsync($"{_baseUrl}/api/tasks/{taskId}/{userId}");
        }
        catch { }
    }

    public async Task RescheduleTaskAsync(int taskId, DateTime newDate, int userId)
    {
        try
        {
            await _http.PostAsJsonAsync($"{_baseUrl}/api/tasks/{taskId}/reschedule/{userId}",
                new { newDate });
        }
        catch { }
    }

    public async Task ScheduleTaskTimeAsync(int taskId, TimeSpan? scheduledTime, int userId, int durationMinutes = 30)
    {
        try
        {
            await _http.PostAsJsonAsync($"{_baseUrl}/api/tasks/{taskId}/schedule-time/{userId}",
                new { scheduledTime, durationMinutes });
        }
        catch { }
    }

    public async Task<List<DailyTask>> GetRecentTasksAsync(int userId, int days = 30)
    {
        var startDate = DateTime.Today.AddDays(-days);
        return await GetTasksForDateRangeAsync(startDate, DateTime.Today, userId);
    }

    public async Task<List<DailyTask>> GetUpcomingTasksAsync(int userId, int days = 14)
    {
        return await GetTasksForDateRangeAsync(DateTime.Today, DateTime.Today.AddDays(days), userId);
    }

    // ==================== PARTICIPANTS ====================

    public async Task<List<Participant>> GetAllParticipantsAsync()
    {
        try
        {
            var participants = await _http.GetFromJsonAsync<List<Participant>>($"{_baseUrl}/api/participants", JsonOptions);
            return participants ?? new List<Participant>();
        }
        catch
        {
            return new List<Participant>();
        }
    }

    public async Task<List<Participant>> GetRecentParticipantsAsync(int userId)
    {
        try
        {
            var participants = await _http.GetFromJsonAsync<List<Participant>>($"{_baseUrl}/api/participants/recent/{userId}", JsonOptions);
            return participants ?? new List<Participant>();
        }
        catch
        {
            return new List<Participant>();
        }
    }

    public async Task<Participant> CreateParticipantAsync(string name, string? email = null, string? phone = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/participants", new { name, email, phone });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<Participant>(JsonOptions);
                if (result != null) return result;
            }
        }
        catch { }
        return new Participant { Name = name, Email = email, Phone = phone };
    }

    public async Task AddParticipantToTaskAsync(int taskId, int participantId, int userId)
    {
        try
        {
            await _http.PostAsync($"{_baseUrl}/api/tasks/{taskId}/participants/{participantId}?userId={userId}", null);
        }
        catch { }
    }

    public async Task RemoveParticipantFromTaskAsync(int taskId, int participantId, int userId)
    {
        try
        {
            await _http.DeleteAsync($"{_baseUrl}/api/tasks/{taskId}/participants/{participantId}?userId={userId}");
        }
        catch { }
    }

    public async Task<List<Participant>> GetTaskParticipantsAsync(int taskId)
    {
        try
        {
            var participants = await _http.GetFromJsonAsync<List<Participant>>($"{_baseUrl}/api/tasks/{taskId}/participants", JsonOptions);
            return participants ?? new List<Participant>();
        }
        catch
        {
            return new List<Participant>();
        }
    }

    // ==================== USER PROFILE ====================

    public async Task UpdateUserProfileAsync(int userId, string? email, string? phone, string? address, string? displayName)
    {
        try
        {
            await _http.PutAsJsonAsync($"{_baseUrl}/api/auth/profile/{userId}", new { email, phone, address, displayName });
        }
        catch { }
    }

    // ==================== HELPERS ====================

    private static User MapUser(UserResponse r) => new()
    {
        Id = r.Id,
        Username = r.Username,
        DisplayName = r.DisplayName,
        Theme = r.Theme,
        IsDailyView = r.IsDailyView,
        UseLargeText = r.UseLargeText
    };

    private static DailyTask MapTask(TaskResponse r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        TaskDate = r.TaskDate,
        IsCompleted = r.IsCompleted,
        SortOrder = r.SortOrder,
        IsAllDay = r.IsAllDay,
        ScheduledTime = r.ScheduledTime,
        DurationMinutes = r.DurationMinutes,
        UserId = r.UserId,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}

// Response models for JSON deserialization
public class UserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string Theme { get; set; } = "teal";
    public bool IsDailyView { get; set; } = true;
    public bool UseLargeText { get; set; } = false;
}

public class TaskResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime TaskDate { get; set; }
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public bool IsAllDay { get; set; }
    public TimeSpan? ScheduledTime { get; set; }
    public int DurationMinutes { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
