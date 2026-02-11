using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class ClaudeService : IClaudeService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    public ClaudeService(HttpClient? httpClient = null, string? apiKey = null, string model = "claude-sonnet-4-20250514")
    {
        _httpClient = httpClient ?? new HttpClient();
        _apiKey = apiKey ?? "";
        _model = model;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
            return false;

        try
        {
            // Send a minimal request to verify the key works
            var response = await CallClaudeAsync("You are a test.", "Reply with OK");
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TaskMetadata?> AugmentTaskAsync(string taskText, List<Participant>? recentParticipants = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        try
        {
            var participantList = recentParticipants != null && recentParticipants.Any()
                ? string.Join(", ", recentParticipants.Select(p => p.Name))
                : "No recent participants";

            var systemPrompt = $@"You are a task metadata assistant. When given a task description, analyze it and return structured metadata in JSON format.

For academic tasks (thesis, homework, study, research, read):
- Set category to ""academic""

For meetings:
- Set category to ""meeting""
- Based on the meeting type and these frequent participants: {participantList}
- Suggest relevant participants who should attend

For personal tasks:
- Set category to ""personal""

For work tasks:
- Set category to ""work""

Always respond with ONLY valid JSON (no markdown, no explanation):
{{
  ""category"": ""academic|meeting|personal|work"",
  ""studentId"": null,
  ""suggestedDueDate"": null,
  ""suggestedParticipants"": [],
  ""aiNotes"": ""brief explanation"",
  ""confidenceScore"": 0.0
}}";

            var response = await CallClaudeAsync(systemPrompt, $"Analyze this task: \"{taskText}\"");
            if (response == null) return null;

            return JsonSerializer.Deserialize<TaskMetadata>(ExtractJson(response), _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<TaskInsights> GetInsightsAsync(List<DailyTask> recentTasks)
    {
        var result = new TaskInsights();

        if (!recentTasks.Any())
        {
            result.ProductivitySummary = "No recent tasks to analyze.";
            return result;
        }

        var completed = recentTasks.Count(t => t.IsCompleted);
        var total = recentTasks.Count;
        result.CompletionRate = total > 0 ? (completed * 100.0 / total) : 0;

        var byDate = recentTasks.GroupBy(t => t.TaskDate.Date).ToList();
        result.AvgTasksPerDay = byDate.Any() ? byDate.Average(g => g.Count()) : 0;

        var completedByDay = recentTasks
            .Where(t => t.IsCompleted)
            .GroupBy(t => t.TaskDate.DayOfWeek)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        result.BestDay = completedByDay?.Key.ToString();

        result.CategoryBreakdown = recentTasks
            .GroupBy(t => t.TaskType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        if (string.IsNullOrEmpty(_apiKey))
        {
            result.ProductivitySummary = $"You've completed {completed} of {total} tasks ({result.CompletionRate:F0}% completion rate).";
            return result;
        }

        try
        {
            var taskSummary = new
            {
                totalTasks = total,
                completed,
                completionRate = result.CompletionRate,
                avgPerDay = result.AvgTasksPerDay,
                bestDay = result.BestDay,
                categories = result.CategoryBreakdown,
                overdueTasks = recentTasks.Count(t => !t.IsCompleted && t.TaskDate.Date < DateTime.Today)
            };

            var systemPrompt = @"You are a productivity insights assistant. Analyze task patterns and provide suggestions.

Respond with ONLY valid JSON (no markdown):
{
  ""summary"": ""2-3 sentence productivity summary"",
  ""suggestions"": [""suggestion 1"", ""suggestion 2""]
}

Be encouraging but honest. Focus on actionable improvements.";

            var response = await CallClaudeAsync(systemPrompt,
                $"Analyze these task statistics from the past 30 days:\n{JsonSerializer.Serialize(taskSummary)}");

            if (response != null)
            {
                var parsed = JsonSerializer.Deserialize<InsightsResponse>(ExtractJson(response), _jsonOptions);
                if (parsed != null)
                {
                    result.ProductivitySummary = parsed.Summary ?? $"Completion rate: {result.CompletionRate:F0}%";
                    result.Suggestions = parsed.Suggestions ?? new List<string>();
                }
            }
        }
        catch
        {
            result.ProductivitySummary = $"You've completed {completed} of {total} tasks ({result.CompletionRate:F0}% completion rate).";
        }

        return result;
    }

    public async Task<string?> SendPromptAsync(string systemPrompt, string userPrompt)
    {
        return await CallClaudeAsync(systemPrompt, userPrompt);
    }

    private async Task<string?> CallClaudeAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var request = new ClaudeRequest
        {
            Model = _model,
            MaxTokens = 1024,
            System = systemPrompt,
            Messages = new[]
            {
                new ClaudeMessage { Role = "user", Content = userPrompt }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl);
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
        httpRequest.Content = JsonContent.Create(request, options: _jsonOptions);

        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
            return null;

        var claudeResponse = await response.Content.ReadFromJsonAsync<ClaudeResponse>(_jsonOptions);

        if (claudeResponse?.Content == null || claudeResponse.Content.Length == 0)
            return null;

        return claudeResponse.Content[0].Text;
    }

    /// <summary>
    /// Extract JSON from a response that might contain markdown code blocks
    /// </summary>
    private static string ExtractJson(string text)
    {
        // Remove ```json ... ``` blocks
        var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)```");
        if (jsonBlockMatch.Success)
            return jsonBlockMatch.Groups[1].Value.Trim();

        // Try to find a JSON object
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return text.Substring(firstBrace, lastBrace - firstBrace + 1);

        return text;
    }

    // Claude API DTOs
    private class ClaudeRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("system")]
        public string System { get; set; } = "";

        [JsonPropertyName("messages")]
        public ClaudeMessage[] Messages { get; set; } = Array.Empty<ClaudeMessage>();
    }

    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public ClaudeContent[] Content { get; set; } = Array.Empty<ClaudeContent>();

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
    }

    private class ClaudeContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    private class InsightsResponse
    {
        public string? Summary { get; set; }
        public List<string>? Suggestions { get; set; }
    }

    public async Task<CalendarAnalysis> AnalyzeCalendarPatternsAsync(List<DailyTask> tasks, int daysToAnalyze = 30)
    {
        // Stub implementation - Claude calendar analysis
        var result = new CalendarAnalysis();
        result.Summary = "Calendar analysis via Claude - feature in development.";
        result.GeneratedAt = DateTime.UtcNow;
        return await Task.FromResult(result);
    }

    public async Task<TaskSearchResponse> SearchTasksAsync(string query, List<DailyTask> allTasks)
    {
        var response = new TaskSearchResponse
        {
            TotalSearched = allTasks.Count
        };

        if (!allTasks.Any())
        {
            response.Summary = "No tasks to search.";
            return response;
        }

        // Build a compact task list for the AI
        var taskData = allTasks.Select(t => new
        {
            id = t.Id,
            title = t.Title,
            description = t.Description,
            date = t.TaskDate.ToString("yyyy-MM-dd"),
            completed = t.IsCompleted,
            started = t.IsStarted,
            type = t.TaskType.ToString(),
            recurring = t.RecurrenceType.ToString()
        }).ToList();

        if (string.IsNullOrEmpty(_apiKey))
        {
            // Fallback to simple text search without AI
            var lowerQuery = query.ToLowerInvariant();
            var matches = allTasks
                .Where(t => (t.Title?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                            (t.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false))
                .Take(20)
                .Select(t => new TaskSearchResult
                {
                    Task = t,
                    MatchReason = "Text match",
                    RelevanceScore = 70
                })
                .ToList();

            response.Results = matches;
            response.Summary = $"Found {matches.Count} tasks matching \"{query}\" (text search).";
            return response;
        }

        try
        {
            var systemPrompt = @"You are a task search assistant. Given a natural language query and a list of tasks, identify the most relevant tasks.

Understand the intent behind the query:
- ""meetings last week"" -> find meeting-type tasks from last week
- ""incomplete work tasks"" -> find work tasks that are not completed
- ""thesis"" -> find tasks mentioning thesis or academic work
- ""with John"" -> find tasks where the description mentions John
- ""overdue"" -> find incomplete tasks from the past

Respond with ONLY valid JSON (no markdown):
{
  ""matchingIds"": [1, 2, 3],
  ""reasons"": {""1"": ""reason"", ""2"": ""reason""},
  ""scores"": {""1"": 95, ""2"": 80},
  ""summary"": ""brief summary of what was found""
}

Return at most 20 matches, ordered by relevance. Today is " + DateTime.Today.ToString("yyyy-MM-dd") + ".";

            var userPrompt = $"Query: \"{query}\"\n\nTasks:\n{JsonSerializer.Serialize(taskData)}";

            var aiResponse = await CallClaudeAsync(systemPrompt, userPrompt);
            if (aiResponse != null)
            {
                var parsed = JsonSerializer.Deserialize<SearchAiResponse>(ExtractJson(aiResponse), _jsonOptions);
                if (parsed != null)
                {
                    var taskDict = allTasks.ToDictionary(t => t.Id);
                    foreach (var id in parsed.MatchingIds ?? new List<int>())
                    {
                        if (taskDict.TryGetValue(id, out var task))
                        {
                            response.Results.Add(new TaskSearchResult
                            {
                                Task = task,
                                MatchReason = parsed.Reasons?.GetValueOrDefault(id.ToString(), "") ?? "",
                                RelevanceScore = parsed.Scores?.GetValueOrDefault(id.ToString(), 50) ?? 50
                            });
                        }
                    }
                    response.Summary = parsed.Summary ?? $"Found {response.Results.Count} matching tasks.";
                }
            }
        }
        catch
        {
            // Fallback to simple search on error
            var lowerQuery = query.ToLowerInvariant();
            response.Results = allTasks
                .Where(t => (t.Title?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                            (t.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false))
                .Take(20)
                .Select(t => new TaskSearchResult
                {
                    Task = t,
                    MatchReason = "Text match",
                    RelevanceScore = 70
                })
                .ToList();
            response.Summary = $"Found {response.Results.Count} tasks (fallback search).";
        }

        return response;
    }

    private class SearchAiResponse
    {
        public List<int>? MatchingIds { get; set; }
        public Dictionary<string, string>? Reasons { get; set; }
        public Dictionary<string, int>? Scores { get; set; }
        public string? Summary { get; set; }
    }
}
