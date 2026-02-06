using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class PromptTemplateService : IPromptTemplateService
{
    private readonly PfdDbContext _context;

    // Built-in default templates
    private static readonly Dictionary<PromptCategory, PromptTemplate> _builtInTemplates = new()
    {
        [PromptCategory.Insights] = new PromptTemplate
        {
            Name = "Default Insights",
            Category = PromptCategory.Insights,
            IsBuiltIn = true,
            Description = "Balanced productivity analysis with actionable suggestions",
            SystemPrompt = @"You are a productivity insights assistant. Analyze task patterns and provide suggestions.

Focus ONLY on the raw task data provided. Do not reference any previous analyses or AI outputs.

Consider:
- Completion rates and trends
- Task distribution across categories
- Overdue patterns
- Daily/weekly rhythms

Respond with valid JSON:
{
  ""summary"": ""2-3 sentence productivity summary based on the data"",
  ""suggestions"": [""actionable suggestion 1"", ""actionable suggestion 2""]
}

Be encouraging but honest. Focus on actionable improvements based on the actual numbers."
        },

        [PromptCategory.Prioritization] = new PromptTemplate
        {
            Name = "Default Prioritization",
            Category = PromptCategory.Prioritization,
            IsBuiltIn = true,
            Description = "Priority based on urgency and task type",
            SystemPrompt = @"You are a task prioritization assistant. Analyze the given tasks and suggest priority order.

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

Priority scores: 90-100=critical, 70-89=high, 50-69=medium, 0-49=low"
        },

        [PromptCategory.Scheduling] = new PromptTemplate
        {
            Name = "Default Scheduling",
            Category = PromptCategory.Scheduling,
            IsBuiltIn = true,
            Description = "Workload-balanced scheduling suggestions",
            SystemPrompt = @"You are a task scheduling assistant. Suggest the best date for a new task.

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
}"
        },

        [PromptCategory.CalendarAnalysis] = new PromptTemplate
        {
            Name = "Default Calendar Analysis",
            Category = PromptCategory.CalendarAnalysis,
            IsBuiltIn = true,
            Description = "Deep calendar pattern analysis",
            SystemPrompt = @"You are a calendar analysis assistant. Analyze calendar patterns to identify:

1. PRIORITIES: What actually gets scheduled vs what gets postponed
2. WORKLOAD: Meeting density, after-hours work, busy vs light days
3. ROUTINES: Repeating events, best times for deep work
4. COORDINATION: Back-to-back meetings, rescheduling patterns

Focus ONLY on the raw task data provided. Never reference previous analyses.

Respond with valid JSON:
{
  ""summary"": ""Overall calendar health assessment"",
  ""priorities"": {
    ""regularlyScheduled"": [""category or task type""],
    ""frequentlyPostponed"": [""category or task type""],
    ""insight"": ""What this reveals about true priorities""
  },
  ""workload"": {
    ""level"": ""light|balanced|heavy|overloaded"",
    ""heavyDays"": [""Monday"", ...],
    ""lightDays"": [""Saturday"", ...],
    ""hasAfterHoursWork"": true/false,
    ""assessment"": ""Workload balance observation""
  },
  ""routines"": {
    ""detected"": [{""name"": ""routine"", ""frequency"": ""daily/weekly""}],
    ""peakProductivityTime"": ""morning/afternoon/evening"",
    ""insight"": ""Productivity rhythm observation""
  },
  ""recommendations"": [
    {""title"": ""recommendation"", ""description"": ""details"", ""priority"": ""high/medium/low""}
  ]
}"
        },

        [PromptCategory.TaskAugmentation] = new PromptTemplate
        {
            Name = "Default Task Augmentation",
            Category = PromptCategory.TaskAugmentation,
            IsBuiltIn = true,
            Description = "Auto-categorize and enhance task metadata",
            SystemPrompt = @"You are a task metadata assistant. When given a task description, analyze it and return structured metadata.

For academic tasks (thesis, homework, study, research, read):
- Set category to ""academic""

For meetings:
- Set category to ""meeting""
- Suggest relevant participants based on context

For personal tasks:
- Set category to ""personal""

For work tasks:
- Set category to ""work""

Respond with ONLY valid JSON:
{
  ""category"": ""academic|meeting|personal|work"",
  ""studentId"": null,
  ""suggestedDueDate"": null,
  ""suggestedParticipants"": [],
  ""aiNotes"": ""brief explanation"",
  ""confidenceScore"": 0.0-1.0
}"
        }
    };

    public PromptTemplateService(PfdDbContext context)
    {
        _context = context;
    }

    public Dictionary<PromptCategory, PromptTemplate> GetBuiltInTemplates() => _builtInTemplates;

    public async Task<List<PromptTemplate>> GetTemplatesAsync(PromptCategory category)
    {
        var dbTemplates = await _context.Set<PromptTemplate>()
            .Where(t => t.Category == category)
            .OrderByDescending(t => t.IsBuiltIn)
            .ThenBy(t => t.Name)
            .ToListAsync();

        // Add built-in if not in DB
        if (!dbTemplates.Any(t => t.IsBuiltIn) && _builtInTemplates.ContainsKey(category))
        {
            dbTemplates.Insert(0, _builtInTemplates[category]);
        }

        return dbTemplates;
    }

    public async Task<PromptTemplate> GetActiveTemplateAsync(PromptCategory category)
    {
        // Check for user-set active template
        var activeTemplate = await _context.Set<PromptTemplate>()
            .Where(t => t.Category == category && t.IsActive)
            .FirstOrDefaultAsync();

        if (activeTemplate != null)
            return activeTemplate;

        // Return built-in default
        return _builtInTemplates.GetValueOrDefault(category) ?? _builtInTemplates[PromptCategory.Insights];
    }

    public async Task SetActiveTemplateAsync(int templateId)
    {
        var template = await _context.Set<PromptTemplate>().FindAsync(templateId);
        if (template == null) return;

        // Deactivate all other templates in this category
        var otherTemplates = await _context.Set<PromptTemplate>()
            .Where(t => t.Category == template.Category && t.Id != templateId)
            .ToListAsync();

        foreach (var t in otherTemplates)
        {
            t.IsActive = false;
        }

        template.IsActive = true;
        await _context.SaveChangesAsync();
    }

    public async Task<PromptTemplate> CreateTemplateAsync(PromptTemplate template)
    {
        template.IsBuiltIn = false;
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;

        _context.Set<PromptTemplate>().Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task UpdateTemplateAsync(PromptTemplate template)
    {
        var existing = await _context.Set<PromptTemplate>().FindAsync(template.Id);
        if (existing == null || existing.IsBuiltIn) return;

        existing.Name = template.Name;
        existing.SystemPrompt = template.SystemPrompt;
        existing.Description = template.Description;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteTemplateAsync(int templateId)
    {
        var template = await _context.Set<PromptTemplate>().FindAsync(templateId);
        if (template == null || template.IsBuiltIn) return false;

        _context.Set<PromptTemplate>().Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task ResetToDefaultAsync(PromptCategory category)
    {
        // Deactivate all custom templates in this category
        var templates = await _context.Set<PromptTemplate>()
            .Where(t => t.Category == category)
            .ToListAsync();

        foreach (var t in templates)
        {
            t.IsActive = false;
        }

        await _context.SaveChangesAsync();
    }
}
