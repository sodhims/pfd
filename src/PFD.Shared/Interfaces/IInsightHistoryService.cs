using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IInsightHistoryService
{
    /// <summary>
    /// Get the most recent insight for comparison
    /// </summary>
    Task<InsightHistory?> GetLatestInsightAsync(int userId, string insightType);

    /// <summary>
    /// Get insights for a date range
    /// </summary>
    Task<List<InsightHistory>> GetInsightHistoryAsync(int userId, string insightType, DateTime? from = null, DateTime? to = null, int limit = 10);

    /// <summary>
    /// Save a new insight with its raw data snapshot
    /// </summary>
    Task<InsightHistory> SaveInsightAsync(int userId, string insightType, DateTime periodStart, DateTime periodEnd,
        TaskDataSnapshot rawData, string? aiInsight, string? aiSuggestions, int? promptTemplateId = null, string? aiModel = null);

    /// <summary>
    /// Generate a raw data snapshot from current tasks (no AI involved)
    /// </summary>
    Task<TaskDataSnapshot> GenerateSnapshotAsync(int userId, DateTime periodStart, DateTime periodEnd);

    /// <summary>
    /// Get the previous snapshot for comparison (returns raw data, not AI text)
    /// </summary>
    Task<TaskDataSnapshot?> GetPreviousSnapshotAsync(int userId, string insightType);

    /// <summary>
    /// Build a comparison summary between two snapshots (for AI context)
    /// </summary>
    string BuildComparisonContext(TaskDataSnapshot current, TaskDataSnapshot? previous);

    /// <summary>
    /// Delete old insights beyond retention period
    /// </summary>
    Task CleanupOldInsightsAsync(int userId, int retentionDays = 90);
}
