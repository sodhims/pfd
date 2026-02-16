namespace PFD.Shared.Models;

/// <summary>
/// Represents a voice recording clip that can be transcribed and converted to a task.
/// Similar to VoicePal's Clip model.
/// </summary>
public class VoiceClip
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Audio data stored as base64 or blob
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = "audio/webm";
    public double DurationSeconds { get; set; }

    // Transcription (null until transcribed)
    public string? TranscribedText { get; set; }
    public string? EditedText { get; set; }
    public double? Confidence { get; set; }
    public string? Locale { get; set; } = "en-US";
    public DateTime? TranscribedAt { get; set; }

    // Task association (null until converted to task)
    public int? TaskId { get; set; }
    public DateTime? ConvertedToTaskAt { get; set; }

    // Status
    public VoiceClipStatus Status { get; set; } = VoiceClipStatus.Recorded;

    // Helper property
    public string EffectiveText => EditedText ?? TranscribedText ?? "";
}

public enum VoiceClipStatus
{
    Recorded = 0,
    Transcribing = 1,
    Transcribed = 2,
    ConvertedToTask = 3,
    Error = 4
}

// Request models for voice clip API
public class SaveVoiceClipRequest
{
    public int UserId { get; set; }
    public string AudioBase64 { get; set; } = "";
    public string? MimeType { get; set; } = "audio/webm";
    public double DurationSeconds { get; set; }
}

public class UpdateClipTextRequest
{
    public string EditedText { get; set; } = "";
}
