using PFD.Shared.Models;

namespace PFD.Blazor.Services;

/// <summary>
/// Service for transcribing audio using Azure Speech Services.
/// </summary>
public interface IAzureSpeechService
{
    /// <summary>
    /// Whether Azure Speech is configured (key and region set).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Tests the connection to Azure Speech Services.
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync();

    /// <summary>
    /// Transcribes audio data to text.
    /// </summary>
    /// <param name="audioData">Raw audio bytes (WAV or WebM format).</param>
    /// <param name="mimeType">MIME type of the audio.</param>
    /// <param name="locale">Language/locale for recognition.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TranscribeAudioResponse> TranscribeAsync(
        byte[] audioData,
        string mimeType = "audio/webm",
        string? locale = null,
        CancellationToken ct = default);
}
