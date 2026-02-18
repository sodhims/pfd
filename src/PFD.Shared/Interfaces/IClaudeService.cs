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
    /// Deep calendar pattern analysis - examines priorities, workload, routines, and coordination
    /// </summary>
    Task<CalendarAnalysis> AnalyzeCalendarPatternsAsync(List<DailyTask> tasks, int daysToAnalyze = 30);

    /// <summary>
    /// Generic prompt - send any prompt and get a text response
    /// </summary>
    Task<string?> SendPromptAsync(string systemPrompt, string userPrompt);

    /// <summary>
    /// AI-powered semantic search across all tasks
    /// </summary>
    Task<TaskSearchResponse> SearchTasksAsync(string query, List<DailyTask> allTasks);

    /// <summary>
    /// Find tasks semantically similar to a new task being created
    /// Returns task IDs of similar tasks with relevance scores
    /// </summary>
    Task<List<SimilarTaskMatch>> FindSimilarTasksAsync(string newTaskTitle, List<DailyTask> existingTasks);

    /// <summary>
    /// AI-powered sorting of waiting tasks based on different strategies
    /// </summary>
    Task<List<SortedTaskResult>> SortWaitingTasksAsync(List<DailyTask> tasks, WaitingSortStrategy strategy);
}

public class SimilarTaskMatch
{
    public int TaskId { get; set; }
    public int RelevanceScore { get; set; } // 0-100
    public string Reason { get; set; } = "";
}

public class SortedTaskResult
{
    public int TaskId { get; set; }
    public int Rank { get; set; }
    public string Reason { get; set; } = "";
}

public enum WaitingSortStrategy
{
    Manual,        // No AI sorting, use default order
    Oldest,        // Oldest first (by days in queue)
    Newest,        // Newest first (by days in queue)
    DueDate,       // By due date (earliest first)
    Priority,      // AI: Urgency/importance based on task text
    QuickWins,     // AI: Effort estimate - quick tasks first
    Context,       // AI: Group similar tasks together
    Staleness      // AI: Things that become irrelevant if delayed
}
