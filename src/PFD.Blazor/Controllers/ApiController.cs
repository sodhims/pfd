using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;
using PFD.Shared.Enums;
using PFD.Services;
using PFD.Blazor.Services;
using PFD.Data;

namespace PFD.Blazor.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IAuthService _authService;
    private readonly IGroupService _groupService;
    private readonly IClaudeService _claudeService;
    private readonly IAzureSpeechService? _speechService;
    private readonly IVoiceClipService? _voiceClipService;
    private readonly IDbContextFactory<PfdDbContext> _dbFactory;

    public ApiController(
        ITaskService taskService,
        IAuthService authService,
        IGroupService groupService,
        IClaudeService claudeService,
        IDbContextFactory<PfdDbContext> dbFactory,
        IAzureSpeechService? speechService = null,
        IVoiceClipService? voiceClipService = null)
    {
        _taskService = taskService;
        _authService = authService;
        _groupService = groupService;
        _claudeService = claudeService;
        _dbFactory = dbFactory;
        _speechService = speechService;
        _voiceClipService = voiceClipService;
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

    // ==================== TASK WORKFLOW ====================

    [HttpPost("tasks/{userId}/process-transitions")]
    public async Task<IActionResult> ProcessDailyTaskTransitions(int userId)
    {
        var count = await _taskService.ProcessDailyTaskTransitionsAsync(userId);
        return Ok(count);
    }

    [HttpPost("tasks/{taskId}/move-to-tasks/{userId}")]
    public async Task<IActionResult> MoveWaitingToTasks(int taskId, int userId)
    {
        var task = await _taskService.MoveWaitingToTasksAsync(taskId, userId);
        if (task == null) return NotFound();
        return Ok(task);
    }

    [HttpGet("tasks/{userId}/long-queue")]
    public async Task<IActionResult> GetLongQueueTasks(int userId, [FromQuery] int daysThreshold = 2)
    {
        var tasks = await _taskService.GetLongQueueTasksAsync(userId, daysThreshold);
        return Ok(tasks);
    }

    [HttpPost("tasks/{userId}/cleanup-recurring")]
    public async Task<IActionResult> CleanupIncompleteRecurringTasks(int userId)
    {
        var count = await _taskService.CleanupIncompleteRecurringTasksAsync(userId);
        return Ok(count);
    }

    // ==================== VOICE-TO-TASK (VoicePal Integration) ====================

    /// <summary>
    /// Creates a task from a voice transcription (VoicePal integration).
    /// Uses AI to extract task title, due date, category, and participants.
    /// </summary>
    [HttpPost("voice-task")]
    public async Task<IActionResult> CreateVoiceTask([FromBody] VoiceTaskRequest request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.TranscribedText))
            return BadRequest(VoiceTaskResponse.Failed("Transcribed text is required", "EMPTY_TEXT"));

        // Verify user exists
        var user = await _authService.GetUserByIdAsync(request.UserId);
        if (user == null)
            return NotFound(VoiceTaskResponse.Failed("User not found", "USER_NOT_FOUND"));

        // Get recent participants for AI context
        var recentParticipants = await _taskService.GetRecentParticipantsAsync(10);

        // Use Claude AI to augment the task (extract title, due date, category, participants)
        TaskMetadata? metadata = null;
        string title = request.TranscribedText;
        DateTime taskDate = DateTime.Today;
        TimeSpan? scheduledTime = null;
        TaskType taskType = TaskType.General;

        try
        {
            if (await _claudeService.IsAvailableAsync())
            {
                metadata = await _claudeService.AugmentTaskAsync(request.TranscribedText, recentParticipants);

                if (metadata != null)
                {
                    // Use AI-suggested due date as task date if available
                    if (metadata.SuggestedDueDate.HasValue)
                    {
                        taskDate = metadata.SuggestedDueDate.Value.Date;
                        // Extract time if present
                        var time = metadata.SuggestedDueDate.Value.TimeOfDay;
                        if (time != TimeSpan.Zero)
                        {
                            scheduledTime = time;
                        }
                    }

                    // Map category to TaskType
                    if (!string.IsNullOrEmpty(metadata.Category))
                    {
                        taskType = metadata.Category.ToLowerInvariant() switch
                        {
                            "meeting" => TaskType.Meeting,
                            "academic" => TaskType.Academic,
                            "personal" => TaskType.Personal,
                            "work" => TaskType.Work,
                            _ => TaskType.General
                        };
                    }

                    // Create a shorter title from the transcription (first sentence or truncate)
                    title = CreateTaskTitle(request.TranscribedText);
                }
            }
        }
        catch
        {
            // If AI fails, continue with basic task creation
        }

        // Create the task
        var task = new DailyTask
        {
            Title = title,
            Description = $"[Voice transcription]\n{request.TranscribedText}",
            TaskDate = taskDate,
            ScheduledTime = scheduledTime,
            IsAllDay = scheduledTime == null,
            DurationMinutes = 30,
            TaskType = taskType,
            UserId = request.UserId,
            DueBy = metadata?.SuggestedDueDate,
            MetadataJson = metadata != null ? System.Text.Json.JsonSerializer.Serialize(metadata) : null
        };

        var created = await _taskService.CreateTaskAsync(task);

        // Return response
        return Ok(new VoiceTaskResponse
        {
            TaskId = created.Id,
            Title = created.Title,
            Description = created.Description,
            TaskDate = created.TaskDate,
            ScheduledTime = created.ScheduledTime,
            DurationMinutes = created.DurationMinutes,
            DueBy = created.DueBy,
            Category = metadata?.Category ?? "General",
            SuggestedParticipants = metadata?.SuggestedParticipants,
            AiConfidence = metadata?.ConfidenceScore ?? 0,
            AiNotes = metadata?.AiNotes,
            Success = true
        });
    }

    /// <summary>
    /// Creates a task title from transcribed text (first sentence or truncated).
    /// </summary>
    private static string CreateTaskTitle(string transcribedText)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
            return "Voice task";

        // Find first sentence
        var sentenceEnders = new[] { '.', '!', '?' };
        var firstSentenceEnd = transcribedText.IndexOfAny(sentenceEnders);

        string title;
        if (firstSentenceEnd > 0 && firstSentenceEnd < 100)
        {
            title = transcribedText[..(firstSentenceEnd + 1)].Trim();
        }
        else if (transcribedText.Length <= 100)
        {
            title = transcribedText.Trim();
        }
        else
        {
            // Truncate at word boundary
            var truncated = transcribedText[..100];
            var lastSpace = truncated.LastIndexOf(' ');
            title = lastSpace > 50 ? truncated[..lastSpace] + "..." : truncated + "...";
        }

        return title;
    }

    // ==================== VOICE TRANSCRIPTION ====================

    /// <summary>
    /// Debug endpoint to check environment variables.
    /// </summary>
    [HttpGet("voice-debug")]
    public IActionResult VoiceDebug()
    {
        var envKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var envRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");

        return Ok(new
        {
            speechServiceRegistered = _speechService != null,
            speechServiceConfigured = _speechService?.IsConfigured ?? false,
            envKeySet = !string.IsNullOrEmpty(envKey),
            envKeyLength = envKey?.Length ?? 0,
            envRegion = envRegion ?? "(not set)"
        });
    }

    /// <summary>
    /// Tests Azure Speech Service connection and shows diagnostic info.
    /// </summary>
    [HttpGet("voice-test")]
    public async Task<IActionResult> TestVoiceConnection([FromServices] IConfiguration config)
    {
        // Check ALL possible sources for the key
        var configKey = config["AZURE_SPEECH_KEY"];
        var configNestedKey = config["AzureSpeech:Key"];
        var envKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var configRegion = config["AZURE_SPEECH_REGION"] ?? config["AzureSpeech:Region"];
        var envRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");

        var diagnostics = new {
            configKeySet = !string.IsNullOrEmpty(configKey),
            configKeyLen = configKey?.Length ?? 0,
            configNestedKeySet = !string.IsNullOrEmpty(configNestedKey),
            envKeySet = !string.IsNullOrEmpty(envKey),
            envKeyLen = envKey?.Length ?? 0,
            configRegion = configRegion ?? "(not set)",
            envRegion = envRegion ?? "(not set)",
            allConfigKeys = config.AsEnumerable()
                .Where(x => x.Key.Contains("AZURE", StringComparison.OrdinalIgnoreCase) ||
                           x.Key.Contains("Speech", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key)
                .ToList()
        };

        if (_speechService == null)
        {
            return Ok(new {
                success = false,
                message = "Azure Speech Service not registered at startup. Check Application Settings.",
                diagnostics
            });
        }

        if (!_speechService.IsConfigured)
        {
            return Ok(new {
                success = false,
                message = "Azure Speech Service not configured. Key or region is missing.",
                diagnostics
            });
        }

        var result = await _speechService.TestConnectionAsync();
        return Ok(new { success = result.Success, message = result.Message });
    }

    /// <summary>
    /// Transcribes audio data to text using Azure Speech Services.
    /// </summary>
    [HttpPost("voice-transcribe")]
    public async Task<IActionResult> TranscribeAudio([FromBody] TranscribeAudioRequest request)
    {
        if (_speechService == null || !_speechService.IsConfigured)
        {
            return Ok(TranscribeAudioResponse.Failed(
                "Voice transcription not configured. Please set Azure Speech credentials.",
                "NOT_CONFIGURED"));
        }

        if (string.IsNullOrEmpty(request.AudioBase64))
        {
            return BadRequest(TranscribeAudioResponse.Failed("No audio data provided", "EMPTY_AUDIO"));
        }

        try
        {
            var audioBytes = Convert.FromBase64String(request.AudioBase64);
            var result = await _speechService.TranscribeAsync(
                audioBytes,
                request.MimeType,
                request.Locale);

            return Ok(result);
        }
        catch (FormatException)
        {
            return BadRequest(TranscribeAudioResponse.Failed("Invalid base64 audio data", "INVALID_BASE64"));
        }
        catch (Exception ex)
        {
            return Ok(TranscribeAudioResponse.Failed($"Transcription failed: {ex.Message}", "TRANSCRIPTION_ERROR"));
        }
    }

    // ==================== VOICE CLIPS ====================

    /// <summary>
    /// Initialize voice clips table (call if table doesn't exist).
    /// </summary>
    [HttpGet("voice-clips-init")]
    public async Task<IActionResult> InitVoiceClipsTable()
    {
        try
        {
            using var db = await _dbFactory.CreateDbContextAsync();

            // Try to query the table first
            try
            {
                var count = await db.VoiceClips.CountAsync();
                return Ok(new { success = true, message = $"Voice clips table exists with {count} clips." });
            }
            catch
            {
                // Table doesn't exist, trigger schema creation
                db.Database.EnsureCreated();
                return Ok(new { success = true, message = "Voice clips table created." });
            }
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Save a recorded voice clip for later transcription.
    /// </summary>
    [HttpPost("voice-clip")]
    public async Task<IActionResult> SaveVoiceClip([FromBody] SaveVoiceClipRequest request)
    {
        if (_voiceClipService == null)
        {
            return Ok(new { success = false, error = "Voice clip service not available" });
        }

        if (string.IsNullOrEmpty(request.AudioBase64))
        {
            return BadRequest(new { success = false, error = "No audio data provided" });
        }

        try
        {
            var audioBytes = Convert.FromBase64String(request.AudioBase64);
            var clip = await _voiceClipService.SaveClipAsync(
                request.UserId,
                audioBytes,
                request.MimeType ?? "audio/webm",
                request.DurationSeconds);

            return Ok(new
            {
                success = true,
                clip = new
                {
                    clip.Id,
                    clip.CreatedAt,
                    clip.DurationSeconds,
                    clip.MimeType,
                    status = clip.Status.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            // Get the innermost exception for better error details
            var innerEx = ex;
            while (innerEx.InnerException != null)
                innerEx = innerEx.InnerException;

            return Ok(new { success = false, error = ex.Message, innerError = innerEx.Message });
        }
    }

    /// <summary>
    /// Get user's voice clips.
    /// </summary>
    [HttpGet("voice-clips/{userId}")]
    public async Task<IActionResult> GetVoiceClips(int userId, [FromQuery] int limit = 20)
    {
        if (_voiceClipService == null)
        {
            return Ok(new { success = false, clips = Array.Empty<object>() });
        }

        var clips = await _voiceClipService.GetUserClipsAsync(userId, limit);
        return Ok(new
        {
            success = true,
            clips = clips.Select(c => new
            {
                c.Id,
                c.CreatedAt,
                c.DurationSeconds,
                c.MimeType,
                c.TranscribedText,
                c.EditedText,
                effectiveText = c.EffectiveText,
                c.Confidence,
                c.TranscribedAt,
                status = c.Status.ToString()
            })
        });
    }

    /// <summary>
    /// Get audio data for a clip (for playback).
    /// </summary>
    [HttpGet("voice-clip/{clipId}/audio")]
    public async Task<IActionResult> GetVoiceClipAudio(int clipId)
    {
        if (_voiceClipService == null)
        {
            return NotFound();
        }

        var clip = await _voiceClipService.GetClipAsync(clipId);
        if (clip == null)
        {
            return NotFound();
        }

        return File(clip.AudioData, clip.MimeType);
    }

    /// <summary>
    /// Transcribe a saved voice clip.
    /// </summary>
    [HttpPost("voice-clip/{clipId}/transcribe")]
    public async Task<IActionResult> TranscribeVoiceClip(int clipId)
    {
        if (_voiceClipService == null)
        {
            return Ok(new { success = false, error = "Voice clip service not available" });
        }

        var clip = await _voiceClipService.TranscribeClipAsync(clipId);
        if (clip == null)
        {
            return Ok(new { success = false, error = "Clip not found or transcription failed" });
        }

        return Ok(new
        {
            success = clip.Status == VoiceClipStatus.Transcribed,
            clip = new
            {
                clip.Id,
                clip.TranscribedText,
                clip.Confidence,
                clip.TranscribedAt,
                status = clip.Status.ToString()
            }
        });
    }

    /// <summary>
    /// Update the edited transcription text.
    /// </summary>
    [HttpPut("voice-clip/{clipId}/text")]
    public async Task<IActionResult> UpdateVoiceClipText(int clipId, [FromBody] UpdateClipTextRequest request)
    {
        if (_voiceClipService == null)
        {
            return Ok(new { success = false, error = "Voice clip service not available" });
        }

        var clip = await _voiceClipService.UpdateTranscriptionAsync(clipId, request.EditedText);
        if (clip == null)
        {
            return Ok(new { success = false, error = "Clip not found" });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete a voice clip.
    /// </summary>
    [HttpDelete("voice-clip/{clipId}")]
    public async Task<IActionResult> DeleteVoiceClip(int clipId)
    {
        if (_voiceClipService == null)
        {
            return Ok(new { success = false, error = "Voice clip service not available" });
        }

        var result = await _voiceClipService.DeleteClipAsync(clipId);
        return Ok(new { success = result });
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
