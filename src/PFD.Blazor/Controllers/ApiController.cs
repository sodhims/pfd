using Microsoft.AspNetCore.Mvc;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Blazor.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IAuthService _authService;
    private readonly IGroupService _groupService;

    public ApiController(ITaskService taskService, IAuthService authService, IGroupService groupService)
    {
        _taskService = taskService;
        _authService = authService;
        _groupService = groupService;
    }

    // ==================== AUTH ====================

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password required" });

        var user = await _authService.LoginAsync(request.Username.Trim(), request.Password);
        if (user == null)
            return Unauthorized(new { error = "Invalid username or password" });

        return Ok(new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Theme = user.Theme,
            IsDailyView = user.IsDailyView,
            UseLargeText = user.UseLargeText
        });
    }

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password required" });

        if (request.Username.Trim().Length < 3)
            return BadRequest(new { error = "Username must be at least 3 characters" });

        if (request.Password.Length < 4)
            return BadRequest(new { error = "Password must be at least 4 characters" });

        if (await _authService.UsernameExistsAsync(request.Username.Trim()))
            return BadRequest(new { error = "Username already taken" });

        var user = await _authService.RegisterAsync(
            request.Username.Trim(),
            request.Password,
            string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim());

        if (user == null)
            return BadRequest(new { error = "Registration failed" });

        return Ok(new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Theme = user.Theme,
            IsDailyView = user.IsDailyView,
            UseLargeText = user.UseLargeText
        });
    }

    [HttpGet("auth/user/{userId}")]
    public async Task<IActionResult> GetUser(int userId)
    {
        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Theme = user.Theme,
            IsDailyView = user.IsDailyView,
            UseLargeText = user.UseLargeText
        });
    }

    [HttpPut("auth/settings/{userId}")]
    public async Task<IActionResult> UpdateSettings(int userId, [FromBody] UpdateSettingsRequest request)
    {
        await _authService.UpdateUserSettingsAsync(userId, request.Theme, request.IsDailyView, request.UseLargeText);
        return Ok();
    }

    // ==================== TASKS ====================

    [HttpGet("tasks/{userId}/date/{date}")]
    public async Task<IActionResult> GetTasksForDate(int userId, DateTime date)
    {
        var tasks = await _taskService.GetTasksForDateAsync(date, userId);
        return Ok(tasks.Select(MapTask));
    }

    [HttpGet("tasks/{userId}/overdue/{beforeDate}")]
    public async Task<IActionResult> GetOverdueTasks(int userId, DateTime beforeDate)
    {
        var tasks = await _taskService.GetOverdueTasksAsync(beforeDate, userId);
        return Ok(tasks.Select(MapTask));
    }

    [HttpGet("tasks/{userId}/range")]
    public async Task<IActionResult> GetTasksForRange(int userId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var tasks = await _taskService.GetTasksForDateRangeAsync(startDate, endDate, userId);
        return Ok(tasks.Select(MapTask));
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var task = new DailyTask
        {
            Title = request.Title,
            TaskDate = request.TaskDate,
            IsCompleted = false,
            SortOrder = request.SortOrder,
            IsAllDay = request.IsAllDay,
            ScheduledTime = request.ScheduledTime,
            DurationMinutes = request.DurationMinutes,
            UserId = request.UserId
        };

        var created = await _taskService.CreateTaskAsync(task);
        return Ok(MapTask(created));
    }

    [HttpPut("tasks/{taskId}")]
    public async Task<IActionResult> UpdateTask(int taskId, [FromBody] UpdateTaskRequest request)
    {
        var task = new DailyTask
        {
            Id = taskId,
            Title = request.Title,
            TaskDate = request.TaskDate,
            IsCompleted = request.IsCompleted,
            SortOrder = request.SortOrder,
            IsAllDay = request.IsAllDay,
            ScheduledTime = request.ScheduledTime,
            DurationMinutes = request.DurationMinutes,
            UserId = request.UserId
        };

        await _taskService.UpdateTaskAsync(task);
        return Ok();
    }

    [HttpPost("tasks/{taskId}/toggle/{userId}")]
    public async Task<IActionResult> ToggleTask(int taskId, int userId)
    {
        var task = await _taskService.ToggleCompletionAsync(taskId, userId);
        return Ok(MapTask(task));
    }

    [HttpDelete("tasks/{taskId}/{userId}")]
    public async Task<IActionResult> DeleteTask(int taskId, int userId)
    {
        await _taskService.DeleteTaskAsync(taskId, userId);
        return Ok();
    }

    [HttpPost("tasks/{taskId}/reschedule/{userId}")]
    public async Task<IActionResult> RescheduleTask(int taskId, int userId, [FromBody] RescheduleRequest request)
    {
        await _taskService.RescheduleTaskAsync(taskId, request.NewDate, userId);
        return Ok();
    }

    [HttpPost("tasks/{taskId}/schedule-time/{userId}")]
    public async Task<IActionResult> ScheduleTaskTime(int taskId, int userId, [FromBody] ScheduleTimeRequest request)
    {
        await _taskService.ScheduleTaskTimeAsync(taskId, request.ScheduledTime, userId, request.DurationMinutes);
        return Ok();
    }

    // ==================== GROUPS ====================

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Group name required" });

        var group = await _groupService.CreateGroupAsync(request.Name.Trim(), request.UserId);
        return Ok(new GroupResponse { Id = group.Id, Name = group.Name, LeaderUserId = group.LeaderUserId });
    }

    [HttpGet("groups/{userId}")]
    public async Task<IActionResult> GetUserGroups(int userId)
    {
        var groups = await _groupService.GetGroupsForUserAsync(userId);
        return Ok(groups.Select(g => new GroupResponse
        {
            Id = g.Id,
            Name = g.Name,
            LeaderUserId = g.LeaderUserId,
            MemberCount = g.Members.Count
        }));
    }

    [HttpGet("groups/{groupId}/members")]
    public async Task<IActionResult> GetGroupMembers(int groupId)
    {
        var members = await _groupService.GetGroupMembersAsync(groupId);
        return Ok(members.Select(m => new GroupMemberResponse
        {
            UserId = m.Id,
            Username = m.Username,
            DisplayName = m.DisplayName
        }));
    }

    [HttpPost("groups/{groupId}/members")]
    public async Task<IActionResult> AddGroupMember(int groupId, [FromBody] AddMemberRequest request)
    {
        var success = await _groupService.AddMemberByUsernameAsync(groupId, request.Username, request.RequestingUserId);
        if (!success)
            return BadRequest(new { error = "Could not add member. User not found, already a member, or you are not the leader." });
        return Ok();
    }

    [HttpDelete("groups/{groupId}/members/{userId}")]
    public async Task<IActionResult> RemoveGroupMember(int groupId, int userId, [FromQuery] int requestingUserId)
    {
        await _groupService.RemoveMemberAsync(groupId, userId, requestingUserId);
        return Ok();
    }

    [HttpDelete("groups/{groupId}")]
    public async Task<IActionResult> DeleteGroup(int groupId, [FromQuery] int userId)
    {
        await _groupService.DeleteGroupAsync(groupId, userId);
        return Ok();
    }

    [HttpPost("tasks/{taskId}/share")]
    public async Task<IActionResult> ShareTask(int taskId, [FromBody] ShareTaskRequest request)
    {
        await _groupService.ShareTaskToGroupAsync(taskId, request.GroupId, request.UserId);
        return Ok();
    }

    [HttpPost("tasks/{taskId}/unshare")]
    public async Task<IActionResult> UnshareTask(int taskId, [FromBody] UnshareTaskRequest request)
    {
        await _groupService.UnshareTaskAsync(taskId, request.UserId);
        return Ok();
    }

    // ==================== HELPERS ====================

    private static TaskResponse MapTask(DailyTask task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        TaskDate = task.TaskDate,
        IsCompleted = task.IsCompleted,
        SortOrder = task.SortOrder,
        IsAllDay = task.IsAllDay,
        ScheduledTime = task.ScheduledTime,
        DurationMinutes = task.DurationMinutes,
        UserId = task.UserId,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        GroupId = task.GroupId,
        GroupName = task.Group?.Name
    };
}

// ==================== REQUEST/RESPONSE MODELS ====================

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class UserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string Theme { get; set; } = "teal";
    public bool IsDailyView { get; set; } = true;
    public bool UseLargeText { get; set; } = false;
}

public class UpdateSettingsRequest
{
    public string Theme { get; set; } = "teal";
    public bool IsDailyView { get; set; } = true;
    public bool UseLargeText { get; set; } = false;
}

public class CreateTaskRequest
{
    public string Title { get; set; } = "";
    public DateTime TaskDate { get; set; }
    public int SortOrder { get; set; }
    public bool IsAllDay { get; set; } = true;
    public TimeSpan? ScheduledTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public int UserId { get; set; }
}

public class UpdateTaskRequest
{
    public string Title { get; set; } = "";
    public DateTime TaskDate { get; set; }
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public bool IsAllDay { get; set; } = true;
    public TimeSpan? ScheduledTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public int UserId { get; set; }
}

public class RescheduleRequest
{
    public DateTime NewDate { get; set; }
}

public class ScheduleTimeRequest
{
    public TimeSpan? ScheduledTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
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
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
}

public class CreateGroupRequest
{
    public string Name { get; set; } = "";
    public int UserId { get; set; }
}

public class GroupResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int LeaderUserId { get; set; }
    public int MemberCount { get; set; }
}

public class GroupMemberResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class AddMemberRequest
{
    public string Username { get; set; } = "";
    public int RequestingUserId { get; set; }
}

public class ShareTaskRequest
{
    public int GroupId { get; set; }
    public int UserId { get; set; }
}

public class UnshareTaskRequest
{
    public int UserId { get; set; }
}
