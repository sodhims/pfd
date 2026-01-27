using System.Net.Http.Json;
using System.Text.Json;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaService(HttpClient? httpClient = null, string baseUrl = "http://localhost:11434", string model = "mistral")
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

    public async Task<TaskMetadata?> AugmentTaskAsync(string taskText, List<Participant>? recentParticipants = null)
    {
        try
        {
            var systemPrompt = BuildSystemPrompt(recentParticipants);
            var userPrompt = $"Analyze this task and provide metadata: \"{taskText}\"";

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
            var content = result?.Message?.Content;

            if (string.IsNullOrEmpty(content))
                return null;

            return JsonSerializer.Deserialize<TaskMetadata>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private string BuildSystemPrompt(List<Participant>? recentParticipants)
    {
        var participantList = recentParticipants != null && recentParticipants.Any()
            ? string.Join(", ", recentParticipants.Select(p => p.Name))
            : "No recent participants";

        return $@"You are a task metadata assistant. When given a task description, analyze it and return structured metadata in JSON format.

For academic tasks (thesis, homework, study, research, read):
- Set category to ""academic""
- If it mentions a student or thesis, suggest a studentId (use 1 as default)
- Suggest a reasonable dueBy date (typically 1-7 days from now)

For meetings:
- Set category to ""meeting""
- Based on the meeting type and these frequent participants: {participantList}
- Suggest relevant participants who should attend

For personal tasks:
- Set category to ""personal""

For work tasks:
- Set category to ""work""

Always respond with valid JSON containing these fields:
{{
  ""category"": ""academic|meeting|personal|work"",
  ""studentId"": null or integer,
  ""suggestedDueDate"": ""ISO date string or null"",
  ""suggestedParticipants"": [""name1"", ""name2""] or [],
  ""aiNotes"": ""brief explanation of suggestions"",
  ""confidenceScore"": 0.0 to 1.0
}}";
    }

    private class OllamaChatResponse
    {
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }
}
