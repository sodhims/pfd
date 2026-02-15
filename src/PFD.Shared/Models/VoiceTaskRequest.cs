namespace PFD.Shared.Models;

/// <summary>
/// Request model for creating a task from a voice transcription (VoicePal integration).
/// </summary>
public class VoiceTaskRequest
{
    /// <summary>
    /// The user ID to create the task for.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The transcribed text from the voice clip.
    /// </summary>
    public string TranscribedText { get; set; } = string.Empty;

    /// <summary>
    /// When the voice clip was recorded (optional).
    /// </summary>
    public DateTime? RecordedAt { get; set; }

    /// <summary>
    /// The VoicePal clip ID for reference (optional).
    /// </summary>
    public string? SourceClipId { get; set; }

    /// <summary>
    /// Transcription confidence score from 0.0 to 1.0 (optional).
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Detected language/locale of the transcription (e.g., "en-US").
    /// </summary>
    public string? DetectedLocale { get; set; }
}
