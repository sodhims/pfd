using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

/// <summary>
/// Stores historical insight snapshots with raw data separate from AI analysis.
/// Raw data can inform future insights without AI-on-AI drift.
/// </summary>
[Table("insight_history")]
public class InsightHistory
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>
    /// Type of insight: daily, weekly, monthly, calendar_analysis, etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string InsightType { get; set; } = "weekly";

    /// <summary>
    /// The date range start for this snapshot
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// The date range end for this snapshot
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// JSON snapshot of raw task statistics at this point in time.
    /// This is what gets referenced for future comparisons.
    /// </summary>
    [Required]
    public string RawDataSnapshot { get; set; } = "{}";

    /// <summary>
    /// The AI-generated insight text (stored but NOT used as input for future analysis)
    /// </summary>
    public string? AiInsightText { get; set; }

    /// <summary>
    /// The AI-generated suggestions as JSON array
    /// </summary>
    public string? AiSuggestions { get; set; }

    /// <summary>
    /// Which prompt template was used for this insight
    /// </summary>
    public int? PromptTemplateId { get; set; }

    /// <summary>
    /// Which AI model generated this insight
    /// </summary>
    [MaxLength(50)]
    public string? AiModel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? User { get; set; }
}

/// <summary>
/// Strongly-typed raw data snapshot for serialization
/// </summary>
public class TaskDataSnapshot
{
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;

    // Counts
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int OverdueTasks { get; set; }

    // Rates
    public double CompletionRate { get; set; }

    // Category breakdown
    public Dictionary<string, int> TasksByCategory { get; set; } = new();
    public Dictionary<string, int> CompletedByCategory { get; set; } = new();

    // Day of week patterns
    public Dictionary<string, int> TasksByDayOfWeek { get; set; } = new();
    public Dictionary<string, int> CompletedByDayOfWeek { get; set; } = new();

    // Time patterns
    public int MorningTasks { get; set; }
    public int AfternoonTasks { get; set; }
    public int EveningTasks { get; set; }

    // Streaks
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    // Meeting stats
    public int MeetingsScheduled { get; set; }
    public int MeetingsCompleted { get; set; }

    // Comparison helpers (filled in when comparing to previous snapshot)
    public double? PreviousCompletionRate { get; set; }
    public int? PreviousTotalTasks { get; set; }
    public string? TrendDirection { get; set; } // "improving", "declining", "stable"
}
