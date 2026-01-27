using System.Net.Http.Json;
using System.Text.Json;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class AnalysisService : IAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public AnalysisService(HttpClient? httpClient = null, string baseUrl = "http://localhost:11434", string model = "mistral")
    {
        _httpClient = httpClient ?? new HttpClient();
        _baseUrl = baseUrl;
        _model = model;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TaskPrioritization> GetPrioritizedTasksAsync(List<DailyTask> tasks, List<DailyTask> overdueTasks)
    {
        var result = new TaskPrioritization();

        if (!tasks.Any() && !overdueTasks.Any())
        {
            result.Summary = "No tasks to prioritize.";
            return result;
        }

        var allTasks = overdueTasks.Concat(tasks.Where(t => !t.IsCompleted)).ToList();

        try
        {
            var taskDescriptions = allTasks.Select((t, i) => new
            {
                id = t.Id,
                title = t.Title,
                type = t.TaskType.ToString(),
                dueBy = t.DueBy?.ToString("yyyy-MM-dd"),
                taskDate = t.TaskDate.ToString("yyyy-MM-dd"),
                isOverdue = t.TaskDate.Date < DateTime.Today,
                daysOverdue = t.TaskDate.Date < DateTime.Today ? (DateTime.Today - t.TaskDate.Date).Days : 0
            }).ToList();

            var systemPrompt = @"You are a task prioritization assistant. Analyze the given tasks and suggest priority order.

Consider these factors:
1. Overdue tasks should generally be highest priority
2. Tasks with due dates approaching soon are higher priority
3. Meeting and work tasks often need immediate attention
4. Academic tasks with deadlines should be prioritized by deadline

Respond with valid JSON:
{
  ""priorityOrder"": [id1, id2, ...],
  ""scores"": {""id1"": 85, ""id2"": 70, ...},
  ""reasoning"": {""id1"": ""reason"", ...},
  ""summary"": ""brief overall recommendation""
}

Priority scores: 90-100=critical, 70-89=high, 50-69=medium, 0-49=low";

            var userPrompt = $"Prioritize these tasks for today ({DateTime.Today:yyyy-MM-dd}):\n{JsonSerializer.Serialize(taskDescriptions)}";

            var response = await CallOllamaAsync(systemPrompt, userPrompt);

            if (response != null)
            {
                var parsed = JsonSerializer.Deserialize<PrioritizationResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed != null)
                {
                    result.PriorityOrder = parsed.PriorityOrder ?? new List<int>();
                    result.PriorityScores = parsed.Scores ?? new Dictionary<int, int>();
                    result.Reasoning = parsed.Reasoning ?? new Dictionary<int, string>();
                    result.Summary = parsed.Summary ?? "Focus on overdue tasks first.";
                }
            }
        }
        catch
        {
            // Fallback: simple priority based on overdue status and due date
            result = GetFallbackPrioritization(allTasks);
        }

        // Ensure we have a fallback if AI failed
        if (!result.PriorityOrder.Any())
        {
            result = GetFallbackPrioritization(allTasks);
        }

        return result;
    }

    public async Task<SchedulingSuggestion> SuggestSchedulingAsync(string taskText, List<DailyTask> existingTasks)
    {
        var result = new SchedulingSuggestion
        {
            SuggestedDate = DateTime.Today,
            Confidence = 0.5
        };

        try
        {
            // Group tasks by date for the next 2 weeks
            var taskCounts = existingTasks
                .Where(t => t.TaskDate >= DateTime.Today && t.TaskDate <= DateTime.Today.AddDays(14))
                .GroupBy(t => t.TaskDate.Date)
                .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count());

            var systemPrompt = @"You are a task scheduling assistant. Suggest the best date for a new task.

Consider:
1. Avoid days with many existing tasks (more than 5 is busy)
2. Urgent-sounding tasks should be scheduled sooner
3. Academic tasks may need buffer time
4. Meetings should be on workdays

Respond with valid JSON:
{
  ""suggestedDate"": ""yyyy-MM-dd"",
  ""alternativeDates"": [""yyyy-MM-dd"", ...],
  ""reasoning"": ""explanation"",
  ""confidence"": 0.0-1.0
}";

            var userPrompt = $@"New task: ""{taskText}""
Today: {DateTime.Today:yyyy-MM-dd}
Task counts for next 2 weeks: {JsonSerializer.Serialize(taskCounts)}

Suggest the best date for this task.";

            var response = await CallOllamaAsync(systemPrompt, userPrompt);

            if (response != null)
            {
                var parsed = JsonSerializer.Deserialize<SchedulingResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed != null)
                {
                    if (DateTime.TryParse(parsed.SuggestedDate, out var date))
                    {
                        result.SuggestedDate = date;
                    }

                    result.AlternativeDates = parsed.AlternativeDates?
                        .Select(d => DateTime.TryParse(d, out var dt) ? dt : DateTime.MinValue)
                        .Where(d => d != DateTime.MinValue)
                        .ToList() ?? new List<DateTime>();

                    result.Reasoning = parsed.Reasoning ?? "Based on workload balance.";
                    result.Confidence = parsed.Confidence;
                }
            }

            // Add task count for suggested date
            result.TasksOnSuggestedDate = existingTasks.Count(t => t.TaskDate.Date == result.SuggestedDate.Date);
        }
        catch
        {
            // Fallback: find the least busy day in the next week
            result = GetFallbackScheduling(existingTasks);
        }

        return result;
    }

    public async Task<TaskInsights> GetInsightsAsync(List<DailyTask> recentTasks)
    {
        var result = new TaskInsights();

        if (!recentTasks.Any())
        {
            result.ProductivitySummary = "No recent tasks to analyze.";
            return result;
        }

        // Calculate basic stats locally
        var completed = recentTasks.Count(t => t.IsCompleted);
        var total = recentTasks.Count;
        result.CompletionRate = total > 0 ? (completed * 100.0 / total) : 0;

        // Group by date and day of week
        var byDate = recentTasks.GroupBy(t => t.TaskDate.Date).ToList();
        result.AvgTasksPerDay = byDate.Any() ? byDate.Average(g => g.Count()) : 0;

        // Find best day (most completions)
        var completedByDay = recentTasks
            .Where(t => t.IsCompleted)
            .GroupBy(t => t.TaskDate.DayOfWeek)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        result.BestDay = completedByDay?.Key.ToString();

        // Category breakdown
        result.CategoryBreakdown = recentTasks
            .GroupBy(t => t.TaskType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        try
        {
            var taskSummary = new
            {
                totalTasks = total,
                completed = completed,
                completionRate = result.CompletionRate,
                avgPerDay = result.AvgTasksPerDay,
                bestDay = result.BestDay,
                categories = result.CategoryBreakdown,
                overdueTasks = recentTasks.Count(t => !t.IsCompleted && t.TaskDate.Date < DateTime.Today)
            };

            var systemPrompt = @"You are a productivity insights assistant. Analyze task patterns and provide suggestions.

Respond with valid JSON:
{
  ""summary"": ""2-3 sentence productivity summary"",
  ""suggestions"": [""suggestion 1"", ""suggestion 2"", ...]
}

Be encouraging but honest. Focus on actionable improvements.";

            var userPrompt = $"Analyze these task statistics from the past 30 days:\n{JsonSerializer.Serialize(taskSummary)}";

            var response = await CallOllamaAsync(systemPrompt, userPrompt);

            if (response != null)
            {
                var parsed = JsonSerializer.Deserialize<InsightsResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed != null)
                {
                    result.ProductivitySummary = parsed.Summary ?? $"Completion rate: {result.CompletionRate:F0}%";
                    result.Suggestions = parsed.Suggestions ?? new List<string>();
                }
            }
        }
        catch
        {
            // Use calculated stats for summary
            result.ProductivitySummary = $"You've completed {completed} of {total} tasks ({result.CompletionRate:F0}% completion rate).";
            result.Suggestions = GetFallbackSuggestions(result);
        }

        if (string.IsNullOrEmpty(result.ProductivitySummary))
        {
            result.ProductivitySummary = $"You've completed {completed} of {total} tasks ({result.CompletionRate:F0}% completion rate).";
        }

        if (!result.Suggestions.Any())
        {
            result.Suggestions = GetFallbackSuggestions(result);
        }

        return result;
    }

    private async Task<string?> CallOllamaAsync(string systemPrompt, string userPrompt)
    {
        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            format = "json"
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/chat", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return result?.Message?.Content;
    }

    private TaskPrioritization GetFallbackPrioritization(List<DailyTask> tasks)
    {
        var result = new TaskPrioritization();

        var prioritized = tasks
            .Select(t => new
            {
                Task = t,
                Score = CalculatePriorityScore(t)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        result.PriorityOrder = prioritized.Select(x => x.Task.Id).ToList();
        result.PriorityScores = prioritized.ToDictionary(x => x.Task.Id, x => x.Score);
        result.Reasoning = prioritized.ToDictionary(
            x => x.Task.Id,
            x => GetPriorityReason(x.Task, x.Score));
        result.Summary = "Prioritized by overdue status and due dates.";

        return result;
    }

    private int CalculatePriorityScore(DailyTask task)
    {
        int score = 50; // Base score

        // Overdue penalty/boost
        if (task.TaskDate.Date < DateTime.Today)
        {
            var daysOverdue = (DateTime.Today - task.TaskDate.Date).Days;
            score += Math.Min(40, daysOverdue * 10); // Up to +40 for overdue
        }

        // Due date urgency
        if (task.DueBy.HasValue)
        {
            var daysUntilDue = (task.DueBy.Value.Date - DateTime.Today).Days;
            if (daysUntilDue <= 0) score += 30;
            else if (daysUntilDue <= 1) score += 20;
            else if (daysUntilDue <= 3) score += 10;
        }

        // Task type weighting
        score += task.TaskType switch
        {
            Shared.Enums.TaskType.Meeting => 15,
            Shared.Enums.TaskType.Work => 10,
            Shared.Enums.TaskType.Academic => 5,
            _ => 0
        };

        return Math.Min(100, score);
    }

    private string GetPriorityReason(DailyTask task, int score)
    {
        if (task.TaskDate.Date < DateTime.Today)
            return $"Overdue by {(DateTime.Today - task.TaskDate.Date).Days} days";

        if (task.DueBy.HasValue && task.DueBy.Value.Date <= DateTime.Today)
            return "Due today";

        if (task.DueBy.HasValue && task.DueBy.Value.Date <= DateTime.Today.AddDays(3))
            return $"Due in {(task.DueBy.Value.Date - DateTime.Today).Days} days";

        return score >= 70 ? "High priority" : "Normal priority";
    }

    private SchedulingSuggestion GetFallbackScheduling(List<DailyTask> existingTasks)
    {
        // Find least busy day in next 7 days
        var dayCounts = Enumerable.Range(0, 7)
            .Select(i => DateTime.Today.AddDays(i))
            .Select(date => new
            {
                Date = date,
                Count = existingTasks.Count(t => t.TaskDate.Date == date)
            })
            .OrderBy(x => x.Count)
            .ToList();

        return new SchedulingSuggestion
        {
            SuggestedDate = dayCounts.First().Date,
            AlternativeDates = dayCounts.Skip(1).Take(2).Select(x => x.Date).ToList(),
            Reasoning = $"This day has the fewest tasks ({dayCounts.First().Count}).",
            Confidence = 0.7,
            TasksOnSuggestedDate = dayCounts.First().Count
        };
    }

    private List<string> GetFallbackSuggestions(TaskInsights insights)
    {
        var suggestions = new List<string>();

        if (insights.CompletionRate < 50)
            suggestions.Add("Try breaking large tasks into smaller, manageable pieces.");

        if (insights.CompletionRate >= 80)
            suggestions.Add("Great completion rate! Consider taking on more challenging goals.");

        if (insights.AvgTasksPerDay > 10)
            suggestions.Add("You're handling many tasks. Consider prioritizing the most important ones.");

        if (insights.AvgTasksPerDay < 2)
            suggestions.Add("Consider adding more tasks to stay organized and productive.");

        if (string.IsNullOrEmpty(insights.BestDay))
            suggestions.Add("Track more tasks to discover your most productive days.");

        return suggestions;
    }

    // Response DTOs
    private class OllamaChatResponse
    {
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        public string? Content { get; set; }
    }

    private class PrioritizationResponse
    {
        public List<int>? PriorityOrder { get; set; }
        public Dictionary<int, int>? Scores { get; set; }
        public Dictionary<int, string>? Reasoning { get; set; }
        public string? Summary { get; set; }
    }

    private class SchedulingResponse
    {
        public string? SuggestedDate { get; set; }
        public List<string>? AlternativeDates { get; set; }
        public string? Reasoning { get; set; }
        public double Confidence { get; set; }
    }

    private class InsightsResponse
    {
        public string? Summary { get; set; }
        public List<string>? Suggestions { get; set; }
    }
}
