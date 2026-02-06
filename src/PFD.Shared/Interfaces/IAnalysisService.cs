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
    /// Get AI insights about task patterns with history tracking
    /// </summary>
    Task<TaskInsights> GetInsightsAsync(List<DailyTask> recentTasks, int userId);

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

/// <summary>
/// Deep calendar pattern analysis results
/// </summary>
public class CalendarAnalysis
{
    /// <summary>
    /// Overall summary of calendar patterns
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Priorities and tradeoffs analysis
    /// </summary>
    public PriorityAnalysis Priorities { get; set; } = new();

    /// <summary>
    /// Workload and balance analysis
    /// </summary>
    public WorkloadAnalysis Workload { get; set; } = new();

    /// <summary>
    /// Routines and productivity rhythms
    /// </summary>
    public RoutineAnalysis Routines { get; set; } = new();

    /// <summary>
    /// Coordination and dependencies analysis
    /// </summary>
    public CoordinationAnalysis Coordination { get; set; } = new();

    /// <summary>
    /// Actionable recommendations
    /// </summary>
    public List<CalendarRecommendation> Recommendations { get; set; } = new();

    /// <summary>
    /// When this analysis was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class PriorityAnalysis
{
    /// <summary>
    /// Tasks/categories that get scheduled regularly (true priorities)
    /// </summary>
    public List<string> RegularlyScheduled { get; set; } = new();

    /// <summary>
    /// Tasks/categories that often get postponed or canceled
    /// </summary>
    public List<string> FrequentlyPostponed { get; set; } = new();

    /// <summary>
    /// Insight about priority patterns
    /// </summary>
    public string Insight { get; set; } = string.Empty;

    /// <summary>
    /// Stated vs actual priority mismatches
    /// </summary>
    public List<string> PriorityMismatches { get; set; } = new();
}

public class WorkloadAnalysis
{
    /// <summary>
    /// Average meeting density per day (hours)
    /// </summary>
    public double AvgMeetingHoursPerDay { get; set; }

    /// <summary>
    /// Average task density per day
    /// </summary>
    public double AvgTasksPerDay { get; set; }

    /// <summary>
    /// Days with heavy workload (6+ hours scheduled)
    /// </summary>
    public List<DayOfWeek> HeavyDays { get; set; } = new();

    /// <summary>
    /// Days with lighter workload (recovery days)
    /// </summary>
    public List<DayOfWeek> LightDays { get; set; } = new();

    /// <summary>
    /// Evening/weekend work detected
    /// </summary>
    public bool HasAfterHoursWork { get; set; }

    /// <summary>
    /// Boundary issues identified
    /// </summary>
    public List<string> BoundaryIssues { get; set; } = new();

    /// <summary>
    /// Overall workload assessment
    /// </summary>
    public string Assessment { get; set; } = string.Empty;

    /// <summary>
    /// Workload level: light, balanced, heavy, overloaded
    /// </summary>
    public string Level { get; set; } = "balanced";
}

public class RoutineAnalysis
{
    /// <summary>
    /// Detected repeating events/habits
    /// </summary>
    public List<DetectedRoutine> Routines { get; set; } = new();

    /// <summary>
    /// Best times for focused/deep work based on meeting patterns
    /// </summary>
    public List<TimeBlock> DeepWorkBlocks { get; set; } = new();

    /// <summary>
    /// Meeting-heavy time blocks
    /// </summary>
    public List<TimeBlock> MeetingBlocks { get; set; } = new();

    /// <summary>
    /// Most productive time of day based on completion patterns
    /// </summary>
    public string PeakProductivityTime { get; set; } = string.Empty;

    /// <summary>
    /// Summary of productivity rhythms
    /// </summary>
    public string Insight { get; set; } = string.Empty;
}

public class DetectedRoutine
{
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty; // daily, weekly, etc.
    public string? TimeOfDay { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public int OccurrenceCount { get; set; }
}

public class TimeBlock
{
    public string Label { get; set; } = string.Empty;
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public List<DayOfWeek> Days { get; set; } = new();
}

public class CoordinationAnalysis
{
    /// <summary>
    /// Number of back-to-back meetings detected
    /// </summary>
    public int BackToBackMeetings { get; set; }

    /// <summary>
    /// Tasks that were frequently rescheduled
    /// </summary>
    public List<string> FrequentlyRescheduled { get; set; } = new();

    /// <summary>
    /// Tasks with long lead times (scheduled far in advance)
    /// </summary>
    public List<string> LongLeadTimeTasks { get; set; } = new();

    /// <summary>
    /// Bottlenecks or friction points identified
    /// </summary>
    public List<string> Bottlenecks { get; set; } = new();

    /// <summary>
    /// Signs of overcommitment
    /// </summary>
    public List<string> OvercommitmentSigns { get; set; } = new();

    /// <summary>
    /// Coordination assessment
    /// </summary>
    public string Assessment { get; set; } = string.Empty;
}

public class CalendarRecommendation
{
    public string Category { get; set; } = string.Empty; // priority, workload, routine, coordination
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium"; // high, medium, low
    public string? ActionLabel { get; set; }
}
