using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IClaudeService
{
    /// <summary>
    /// Check if Claude API is configured and reachable
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Augment a task with AI-generated metadata (category, participants, due date)
    /// </summary>
    Task<TaskMetadata?> AugmentTaskAsync(string taskText, List<Participant>? recentParticipants = null);

    /// <summary>
    /// Get AI insights about task patterns (replaces Ollama-based analysis)
    /// </summary>
    Task<TaskInsights> GetInsightsAsync(List<DailyTask> recentTasks);

    /// <summary>
    /// Generic prompt - send any prompt and get a text response
    /// </summary>
    Task<string?> SendPromptAsync(string systemPrompt, string userPrompt);
}
