using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class TaskTemplateService : ITaskTemplateService
{
    private readonly IDbContextFactory<PfdDbContext> _contextFactory;

    public TaskTemplateService(IDbContextFactory<PfdDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<TaskTemplate>> GetTemplatesAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TaskTemplates
            .Where(t => t.UserId == userId && t.IsActive)
            .Include(t => t.SubtaskTemplates.OrderBy(st => st.SortOrder))
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<TaskTemplate?> GetTemplateByIdAsync(int templateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TaskTemplates
            .Include(t => t.SubtaskTemplates.OrderBy(st => st.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == templateId);
    }

    public async Task<TaskTemplate> CreateTemplateAsync(int userId, string name, string? description = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var maxSortOrder = await context.TaskTemplates
            .Where(t => t.UserId == userId)
            .Select(t => (int?)t.SortOrder)
            .MaxAsync() ?? 0;

        var template = new TaskTemplate
        {
            UserId = userId,
            Name = name,
            Description = description,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TaskTemplates.Add(template);
        await context.SaveChangesAsync();

        return template;
    }

    public async Task<TaskTemplate?> UpdateTemplateAsync(int templateId, string name, string? description = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var template = await context.TaskTemplates.FindAsync(templateId);

        if (template == null) return null;

        template.Name = name;
        template.Description = description;
        template.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteTemplateAsync(int templateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var template = await context.TaskTemplates.FindAsync(templateId);

        if (template == null) return false;

        context.TaskTemplates.Remove(template);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<SubtaskTemplate> AddSubtaskTemplateAsync(int templateId, string title, string? description = null, int? durationMinutes = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var maxSortOrder = await context.SubtaskTemplates
            .Where(st => st.TaskTemplateId == templateId)
            .Select(st => (int?)st.SortOrder)
            .MaxAsync() ?? 0;

        var subtaskTemplate = new SubtaskTemplate
        {
            TaskTemplateId = templateId,
            Title = title,
            Description = description,
            DurationMinutes = durationMinutes,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        context.SubtaskTemplates.Add(subtaskTemplate);
        await context.SaveChangesAsync();

        return subtaskTemplate;
    }

    public async Task<SubtaskTemplate?> UpdateSubtaskTemplateAsync(int subtaskTemplateId, string title, string? description = null, int? durationMinutes = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subtaskTemplate = await context.SubtaskTemplates.FindAsync(subtaskTemplateId);

        if (subtaskTemplate == null) return null;

        subtaskTemplate.Title = title;
        subtaskTemplate.Description = description;
        subtaskTemplate.DurationMinutes = durationMinutes;

        await context.SaveChangesAsync();
        return subtaskTemplate;
    }

    public async Task<bool> RemoveSubtaskTemplateAsync(int subtaskTemplateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subtaskTemplate = await context.SubtaskTemplates.FindAsync(subtaskTemplateId);

        if (subtaskTemplate == null) return false;

        context.SubtaskTemplates.Remove(subtaskTemplate);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task ReorderSubtaskTemplatesAsync(int templateId, List<int> subtaskTemplateIds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subtaskTemplates = await context.SubtaskTemplates
            .Where(st => st.TaskTemplateId == templateId)
            .ToListAsync();

        for (int i = 0; i < subtaskTemplateIds.Count; i++)
        {
            var subtaskTemplate = subtaskTemplates.FirstOrDefault(st => st.Id == subtaskTemplateIds[i]);
            if (subtaskTemplate != null)
            {
                subtaskTemplate.SortOrder = i;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<DailyTask> CreateTaskFromTemplateAsync(int templateId, int userId, DateTime taskDate, string? customTitle = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var template = await context.TaskTemplates
            .Include(t => t.SubtaskTemplates.OrderBy(st => st.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
            throw new ArgumentException($"Template with ID {templateId} not found", nameof(templateId));

        // Get next sort order for the day
        var maxSortOrder = await context.DailyTasks
            .Where(t => t.UserId == userId && t.TaskDate.Date == taskDate.Date && t.ParentTaskId == null)
            .Select(t => (int?)t.SortOrder)
            .MaxAsync() ?? 0;

        // Create the parent task
        var parentTask = new DailyTask
        {
            UserId = userId,
            Title = customTitle ?? template.Name,
            Description = template.Description,
            TaskDate = taskDate.Date,
            TaskType = template.DefaultTaskType,
            DurationMinutes = template.DefaultDurationMinutes,
            IsAllDay = template.DefaultIsAllDay,
            CustomColor = template.DefaultColor,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.DailyTasks.Add(parentTask);
        await context.SaveChangesAsync();

        // Create subtasks from subtask templates
        foreach (var subtaskTemplate in template.SubtaskTemplates)
        {
            var subtask = new DailyTask
            {
                UserId = userId,
                Title = subtaskTemplate.Title,
                Description = subtaskTemplate.Description,
                TaskDate = taskDate.Date,
                ParentTaskId = parentTask.Id,
                DurationMinutes = subtaskTemplate.DurationMinutes ?? 30,
                IsAllDay = true,
                SortOrder = subtaskTemplate.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.DailyTasks.Add(subtask);
        }

        await context.SaveChangesAsync();

        // Reload with subtasks
        return await context.DailyTasks
            .Include(t => t.Subtasks)
            .FirstAsync(t => t.Id == parentTask.Id);
    }
}
