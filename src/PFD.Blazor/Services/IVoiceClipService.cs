using PFD.Shared.Models;

namespace PFD.Blazor.Services;

public interface IVoiceClipService
{
    Task<VoiceClip> SaveClipAsync(int userId, byte[] audioData, string mimeType, double durationSeconds);
    Task<List<VoiceClip>> GetUserClipsAsync(int userId, int limit = 20);
    Task<VoiceClip?> GetClipAsync(int clipId);
    Task<VoiceClip?> TranscribeClipAsync(int clipId);
    Task<VoiceClip?> UpdateTranscriptionAsync(int clipId, string editedText);
    Task<bool> DeleteClipAsync(int clipId);
    Task<VoiceClip?> ConvertToTaskAsync(int clipId, int taskId);
}
