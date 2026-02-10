using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

/// <summary>
/// Service for managing task templates with subtasks.
/// </summary>
public interface ITaskTemplateService
{
    /// <summary>
    /// Gets all active task templates for a user.
    /// </summary>
    Task<List<TaskTemplate>> GetTemplatesAsync(int userId);

    /// <summary>
    /// Gets a template by ID with its subtask templates.
    /// </summary>
    Task<TaskTemplate?> GetTemplateByIdAsync(int templateId);

    /// <summary>
    /// Creates a new task template.
    /// </summary>
    Task<TaskTemplate> CreateTemplateAsync(int userId, string name, string? description = null);

    /// <summary>
    /// Updates an existing task template.
    /// </summary>
    Task<TaskTemplate?> UpdateTemplateAsync(int templateId, string name, string? description = null);

    /// <summary>
    /// Deletes a task template and all its subtask templates.
    /// </summary>
    Task<bool> DeleteTemplateAsync(int templateId);

    /// <summary>
    /// Adds a subtask template to a task template.
    /// </summary>
    Task<SubtaskTemplate> AddSubtaskTemplateAsync(int templateId, string title, string? description = null, int? durationMinutes = null);

    /// <summary>
    /// Updates a subtask template.
    /// </summary>
    Task<SubtaskTemplate?> UpdateSubtaskTemplateAsync(int subtaskTemplateId, string title, string? description = null, int? durationMinutes = null);

    /// <summary>
    /// Removes a subtask template.
    /// </summary>
    Task<bool> RemoveSubtaskTemplateAsync(int subtaskTemplateId);

    /// <summary>
    /// Reorders subtask templates within a template.
    /// </summary>
    Task ReorderSubtaskTemplatesAsync(int templateId, List<int> subtaskTemplateIds);

    /// <summary>
    /// Creates a task from a template, including all subtasks.
    /// </summary>
    /// <param name="templateId">The template to create from</param>
    /// <param name="userId">The user creating the task</param>
    /// <param name="taskDate">The date for the task</param>
    /// <param name="customTitle">Optional custom title (uses template name if null)</param>
    /// <returns>The created parent task with subtasks</returns>
    Task<DailyTask> CreateTaskFromTemplateAsync(int templateId, int userId, DateTime taskDate, string? customTitle = null);
}
