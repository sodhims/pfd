namespace PFD.Shared.Models;

/// <summary>
/// Response model for a task created from a voice transcription.
/// </summary>
public class VoiceTaskResponse
{
    /// <summary>
    /// The ID of the created task.
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// The AI-generated task title (summarized from transcription).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Task description (includes original transcription).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The date the task is scheduled for.
    /// </summary>
    public DateTime TaskDate { get; set; }

    /// <summary>
    /// Scheduled time of day (if extracted from transcription).
    /// </summary>
    public TimeSpan? ScheduledTime { get; set; }

    /// <summary>
    /// Duration in minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Due date/time (if mentioned in transcription).
    /// </summary>
    public DateTime? DueBy { get; set; }

    /// <summary>
    /// AI-determined category (Meeting, Academic, Personal, Work, General).
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Names of participants mentioned in the transcription.
    /// </summary>
    public List<string>? SuggestedParticipants { get; set; }

    /// <summary>
    /// AI confidence score for the extraction (0.0 to 1.0).
    /// </summary>
    public double AiConfidence { get; set; }

    /// <summary>
    /// AI notes explaining the extraction.
    /// </summary>
    public string? AiNotes { get; set; }

    /// <summary>
    /// Whether the task was created successfully.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if creation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic error handling.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static VoiceTaskResponse Failed(string errorMessage, string errorCode) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
