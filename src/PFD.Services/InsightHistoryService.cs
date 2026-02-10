using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class InsightHistoryService : IInsightHistoryService
{
    private readonly IDbContextFactory<PfdDbContext> _contextFactory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public InsightHistoryService(IDbContextFactory<PfdDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<InsightHistory?> GetLatestInsightAsync(int userId, string insightType)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<InsightHistory>()
            .Where(h => h.UserId == userId && h.InsightType == insightType)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<InsightHistory>> GetInsightHistoryAsync(int userId, string insightType,
        DateTime? from = null, DateTime? to = null, int limit = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Set<InsightHistory>()
            .Where(h => h.UserId == userId && h.InsightType == insightType);

        if (from.HasValue)
            query = query.Where(h => h.PeriodStart >= from.Value);
        if (to.HasValue)
            query = query.Where(h => h.PeriodEnd <= to.Value);

        return await query
            .OrderByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<InsightHistory> SaveInsightAsync(int userId, string insightType,
        DateTime periodStart, DateTime periodEnd, TaskDataSnapshot rawData,
        string? aiInsight, string? aiSuggestions, int? promptTemplateId = null, string? aiModel = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var history = new InsightHistory
        {
            UserId = userId,
            InsightType = insightType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RawDataSnapshot = JsonSerializer.Serialize(rawData, _jsonOptions),
            AiInsightText = aiInsight,
            AiSuggestions = aiSuggestions,
            PromptTemplateId = promptTemplateId,
            AiModel = aiModel,
            CreatedAt = DateTime.UtcNow
        };

        context.Set<InsightHistory>().Add(history);
        await context.SaveChangesAsync();

        return history;
    }

    public async Task<TaskDataSnapshot> GenerateSnapshotAsync(int userId, DateTime periodStart, DateTime periodEnd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tasks = await context.DailyTasks
            .Where(t => t.UserId == userId && t.TaskDate >= periodStart && t.TaskDate <= periodEnd)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var snapshot = new TaskDataSnapshot
        {
            SnapshotDate = now,
            TotalTasks = tasks.Count,
            CompletedTasks = tasks.Count(t => t.IsCompleted),
            PendingTasks = tasks.Count(t => !t.IsCompleted && t.TaskDate >= now.Date),
            OverdueTasks = tasks.Count(t => !t.IsCompleted && t.TaskDate < now.Date)
        };

        snapshot.CompletionRate = snapshot.TotalTasks > 0
            ? Math.Round((double)snapshot.CompletedTasks / snapshot.TotalTasks * 100, 1)
            : 0;

        // Category breakdown (using TaskType enum)
        var categories = tasks.GroupBy(t => t.TaskType.ToString());
        foreach (var cat in categories)
        {
            snapshot.TasksByCategory[cat.Key] = cat.Count();
            snapshot.CompletedByCategory[cat.Key] = cat.Count(t => t.IsCompleted);
        }

        // Day of week patterns
        var dayGroups = tasks.GroupBy(t => t.TaskDate.DayOfWeek.ToString());
        foreach (var day in dayGroups)
        {
            snapshot.TasksByDayOfWeek[day.Key] = day.Count();
            snapshot.CompletedByDayOfWeek[day.Key] = day.Count(t => t.IsCompleted);
        }

        // Time-of-day patterns (for tasks with scheduled times)
        var scheduledTasks = tasks.Where(t => t.ScheduledTime.HasValue).ToList();
        snapshot.MorningTasks = scheduledTasks.Count(t => t.ScheduledTime!.Value.Hours < 12);
        snapshot.AfternoonTasks = scheduledTasks.Count(t => t.ScheduledTime!.Value.Hours >= 12 && t.ScheduledTime!.Value.Hours < 17);
        snapshot.EveningTasks = scheduledTasks.Count(t => t.ScheduledTime!.Value.Hours >= 17);

        // Meeting stats (using TaskType.Meeting)
        snapshot.MeetingsScheduled = tasks.Count(t => t.TaskType == Shared.Enums.TaskType.Meeting);
        snapshot.MeetingsCompleted = tasks.Count(t => t.TaskType == Shared.Enums.TaskType.Meeting && t.IsCompleted);

        // Calculate streaks
        snapshot.CurrentStreak = await CalculateCurrentStreakAsync(context, userId);
        snapshot.LongestStreak = await CalculateLongestStreakAsync(context, userId);

        return snapshot;
    }

    public async Task<TaskDataSnapshot?> GetPreviousSnapshotAsync(int userId, string insightType)
    {
        var latest = await GetLatestInsightAsync(userId, insightType);
        if (latest == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<TaskDataSnapshot>(latest.RawDataSnapshot, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public string BuildComparisonContext(TaskDataSnapshot current, TaskDataSnapshot? previous)
    {
        if (previous == null)
            return "This is the first analysis - no historical data for comparison.";

        var comparisons = new List<string>();

        // Completion rate comparison
        var rateChange = current.CompletionRate - previous.CompletionRate;
        if (Math.Abs(rateChange) >= 5)
        {
            var direction = rateChange > 0 ? "improved" : "declined";
            comparisons.Add($"Completion rate has {direction} from {previous.CompletionRate}% to {current.CompletionRate}% ({(rateChange > 0 ? "+" : "")}{rateChange:F1}%)");
        }
        else
        {
            comparisons.Add($"Completion rate is stable at {current.CompletionRate}% (was {previous.CompletionRate}%)");
        }

        // Task volume comparison
        var taskChange = current.TotalTasks - previous.TotalTasks;
        if (taskChange != 0)
        {
            var volumeDir = taskChange > 0 ? "more" : "fewer";
            comparisons.Add($"Workload: {Math.Abs(taskChange)} {volumeDir} tasks this period ({current.TotalTasks} vs {previous.TotalTasks})");
        }

        // Overdue comparison
        if (current.OverdueTasks != previous.OverdueTasks)
        {
            var overdueDir = current.OverdueTasks > previous.OverdueTasks ? "increased" : "decreased";
            comparisons.Add($"Overdue tasks {overdueDir} from {previous.OverdueTasks} to {current.OverdueTasks}");
        }

        // Streak comparison
        if (current.CurrentStreak != previous.CurrentStreak)
        {
            if (current.CurrentStreak > previous.CurrentStreak)
                comparisons.Add($"Streak extended from {previous.CurrentStreak} to {current.CurrentStreak} days");
            else if (current.CurrentStreak == 0 && previous.CurrentStreak > 0)
                comparisons.Add($"Previous {previous.CurrentStreak}-day streak was broken");
        }

        // Determine overall trend
        current.PreviousCompletionRate = previous.CompletionRate;
        current.PreviousTotalTasks = previous.TotalTasks;
        current.TrendDirection = rateChange > 5 ? "improving" : (rateChange < -5 ? "declining" : "stable");

        return $"Historical comparison (based on raw data, not previous AI analysis):\n" +
               string.Join("\n", comparisons.Select(c => $"- {c}"));
    }

    private static async Task<int> CalculateCurrentStreakAsync(PfdDbContext context, int userId)
    {
        var today = DateTime.UtcNow.Date;
        var streak = 0;
        var currentDate = today;

        while (true)
        {
            var tasksForDay = await context.DailyTasks
                .Where(t => t.UserId == userId && t.TaskDate.Date == currentDate)
                .ToListAsync();

            if (!tasksForDay.Any())
            {
                currentDate = currentDate.AddDays(-1);
                if ((today - currentDate).Days > 1)
                    break;
                continue;
            }

            var allCompleted = tasksForDay.All(t => t.IsCompleted);
            if (allCompleted)
            {
                streak++;
                currentDate = currentDate.AddDays(-1);
            }
            else
            {
                break;
            }

            if (streak > 365) break; // Safety limit
        }

        return streak;
    }

    private static async Task<int> CalculateLongestStreakAsync(PfdDbContext context, int userId)
    {
        var tasks = await context.DailyTasks
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.TaskDate)
            .ToListAsync();

        if (!tasks.Any()) return 0;

        var tasksByDate = tasks.GroupBy(t => t.TaskDate.Date)
            .ToDictionary(g => g.Key, g => g.All(t => t.IsCompleted));

        var longestStreak = 0;
        var currentStreak = 0;
        var dates = tasksByDate.Keys.OrderBy(d => d).ToList();

        for (int i = 0; i < dates.Count; i++)
        {
            if (tasksByDate[dates[i]])
            {
                currentStreak++;
                if (i > 0 && (dates[i] - dates[i - 1]).Days > 1)
                    currentStreak = 1; // Gap in dates, reset
            }
            else
            {
                currentStreak = 0;
            }

            longestStreak = Math.Max(longestStreak, currentStreak);
        }

        return longestStreak;
    }

    public async Task CleanupOldInsightsAsync(int userId, int retentionDays = 90)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var oldInsights = await context.Set<InsightHistory>()
            .Where(h => h.UserId == userId && h.CreatedAt < cutoff)
            .ToListAsync();

        if (oldInsights.Any())
        {
            context.Set<InsightHistory>().RemoveRange(oldInsights);
            await context.SaveChangesAsync();
        }
    }
}
