using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Models;

namespace PFD.Blazor.Services;

public class VoiceClipService : IVoiceClipService
{
    private readonly IDbContextFactory<PfdDbContext> _dbFactory;
    private readonly IAzureSpeechService? _speechService;
    private readonly ILogger<VoiceClipService> _logger;

    public VoiceClipService(
        IDbContextFactory<PfdDbContext> dbFactory,
        ILogger<VoiceClipService> logger,
        IAzureSpeechService? speechService = null)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _speechService = speechService;
    }

    public async Task<VoiceClip> SaveClipAsync(int userId, byte[] audioData, string mimeType, double durationSeconds)
    {
        using var db = await _dbFactory.CreateDbContextAsync();

        var clip = new VoiceClip
        {
            UserId = userId,
            AudioData = audioData,
            MimeType = mimeType,
            DurationSeconds = durationSeconds,
            Status = VoiceClipStatus.Recorded,
            CreatedAt = DateTime.UtcNow
        };

        db.VoiceClips.Add(clip);
        await db.SaveChangesAsync();

        _logger.LogInformation("Saved voice clip {ClipId} for user {UserId}, {Bytes} bytes", clip.Id, userId, audioData.Length);
        return clip;
    }

    public async Task<List<VoiceClip>> GetUserClipsAsync(int userId, int limit = 20)
    {
        using var db = await _dbFactory.CreateDbContextAsync();

        return await db.VoiceClips
            .Where(c => c.UserId == userId && c.Status != VoiceClipStatus.ConvertedToTask)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<VoiceClip?> GetClipAsync(int clipId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.VoiceClips.FindAsync(clipId);
    }

    public async Task<VoiceClip?> TranscribeClipAsync(int clipId)
    {
        if (_speechService == null || !_speechService.IsConfigured)
        {
            _logger.LogWarning("Azure Speech not configured, cannot transcribe clip {ClipId}", clipId);
            return null;
        }

        using var db = await _dbFactory.CreateDbContextAsync();
        var clip = await db.VoiceClips.FindAsync(clipId);
        if (clip == null) return null;

        clip.Status = VoiceClipStatus.Transcribing;
        await db.SaveChangesAsync();

        try
        {
            var result = await _speechService.TranscribeAsync(
                clip.AudioData,
                clip.MimeType,
                clip.Locale);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                clip.TranscribedText = result.Text;
                clip.Confidence = result.Confidence;
                clip.Locale = result.DetectedLocale ?? clip.Locale;
                clip.TranscribedAt = DateTime.UtcNow;
                clip.Status = VoiceClipStatus.Transcribed;

                _logger.LogInformation("Transcribed clip {ClipId}: {Text}", clipId, result.Text.Length > 50 ? result.Text[..50] + "..." : result.Text);
            }
            else
            {
                clip.Status = VoiceClipStatus.Error;
                _logger.LogWarning("Transcription failed for clip {ClipId}: {Error}", clipId, result.ErrorMessage);
            }

            await db.SaveChangesAsync();
            return clip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transcribing clip {ClipId}", clipId);
            clip.Status = VoiceClipStatus.Error;
            await db.SaveChangesAsync();
            return clip;
        }
    }

    public async Task<VoiceClip?> UpdateTranscriptionAsync(int clipId, string editedText)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var clip = await db.VoiceClips.FindAsync(clipId);
        if (clip == null) return null;

        clip.EditedText = editedText;
        await db.SaveChangesAsync();
        return clip;
    }

    public async Task<bool> DeleteClipAsync(int clipId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var clip = await db.VoiceClips.FindAsync(clipId);
        if (clip == null) return false;

        db.VoiceClips.Remove(clip);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<VoiceClip?> ConvertToTaskAsync(int clipId, int taskId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var clip = await db.VoiceClips.FindAsync(clipId);
        if (clip == null) return null;

        clip.TaskId = taskId;
        clip.ConvertedToTaskAt = DateTime.UtcNow;
        clip.Status = VoiceClipStatus.ConvertedToTask;

        await db.SaveChangesAsync();
        return clip;
    }
}
