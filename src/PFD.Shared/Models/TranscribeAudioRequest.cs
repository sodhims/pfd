namespace PFD.Shared.Models;

/// <summary>
/// Request model for audio transcription via Azure Speech.
/// </summary>
public class TranscribeAudioRequest
{
    /// <summary>
    /// Base64-encoded audio data.
    /// </summary>
    public string AudioBase64 { get; set; } = "";

    /// <summary>
    /// MIME type of the audio (e.g., "audio/webm", "audio/wav").
    /// </summary>
    public string MimeType { get; set; } = "audio/webm";

    /// <summary>
    /// Locale for speech recognition (e.g., "en-US").
    /// </summary>
    public string? Locale { get; set; } = "en-US";
}

/// <summary>
/// Response model for audio transcription.
/// </summary>
public class TranscribeAudioResponse
{
    /// <summary>
    /// Whether transcription was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The transcribed text.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Detected language/locale.
    /// </summary>
    public string? DetectedLocale { get; set; }

    /// <summary>
    /// Error message if transcription failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static TranscribeAudioResponse Ok(string text, double? confidence = null, string? locale = null) => new()
    {
        Success = true,
        Text = text,
        Confidence = confidence,
        DetectedLocale = locale
    };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static TranscribeAudioResponse Failed(string errorMessage, string errorCode = "TRANSCRIPTION_ERROR") => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
