using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IAnalysisService
{
    /// <summary>
    /// Analyze tasks and suggest priority order
    /// </summary>
    Task<TaskPrioritization> GetPrioritizedTasksAsync(List<DailyTask> tasks, List<DailyTask> overdueTasks);

    /// <summary>
    /// Suggest optimal scheduling for a new task
    /// </summary>
    Task<SchedulingSuggestion> SuggestSchedulingAsync(string taskText, List<DailyTask> existingTasks);

    /// <summary>
    /// Get AI insights about task patterns
    /// </summary>
    Task<TaskInsights> GetInsightsAsync(List<DailyTask> recentTasks);

    /// <summary>
    /// Check if AI service is available
    /// </summary>
    Task<bool> IsAvailableAsync();
}

/// <summary>
/// AI-generated task prioritization
/// </summary>
public class TaskPrioritization
{
    /// <summary>
    /// Task IDs in priority order (highest first)
    /// </summary>
    public List<int> PriorityOrder { get; set; } = new();

    /// <summary>
    /// Priority scores for each task (0-100)
    /// </summary>
    public Dictionary<int, int> PriorityScores { get; set; } = new();

    /// <summary>
    /// AI reasoning for priorities
    /// </summary>
    public Dictionary<int, string> Reasoning { get; set; } = new();

    /// <summary>
    /// Overall recommendation
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// AI scheduling suggestion
/// </summary>
public class SchedulingSuggestion
{
    /// <summary>
    /// Suggested date for the task
    /// </summary>
    public DateTime SuggestedDate { get; set; }

    /// <summary>
    /// Alternative dates if primary is busy
    /// </summary>
    public List<DateTime> AlternativeDates { get; set; } = new();

    /// <summary>
    /// Reasoning for the suggestion
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Confidence in the suggestion (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Tasks on the suggested date (to show workload)
    /// </summary>
    public int TasksOnSuggestedDate { get; set; }
}

/// <summary>
/// AI insights about task patterns
/// </summary>
public class TaskInsights
{
    /// <summary>
    /// Summary of productivity patterns
    /// </summary>
    public string ProductivitySummary { get; set; } = string.Empty;

    /// <summary>
    /// Most productive day of week
    /// </summary>
    public string? BestDay { get; set; }

    /// <summary>
    /// Average tasks completed per day
    /// </summary>
    public double AvgTasksPerDay { get; set; }

    /// <summary>
    /// Completion rate (0-100%)
    /// </summary>
    public double CompletionRate { get; set; }

    /// <summary>
    /// Suggested improvements
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Category breakdown
    /// </summary>
    public Dictionary<string, int> CategoryBreakdown { get; set; } = new();
}
