using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IPromptTemplateService
{
    /// <summary>
    /// Get all templates for a category
    /// </summary>
    Task<List<PromptTemplate>> GetTemplatesAsync(PromptCategory category);

    /// <summary>
    /// Get the active template for a category
    /// </summary>
    Task<PromptTemplate> GetActiveTemplateAsync(PromptCategory category);

    /// <summary>
    /// Set a template as active for its category
    /// </summary>
    Task SetActiveTemplateAsync(int templateId);

    /// <summary>
    /// Create a new custom template
    /// </summary>
    Task<PromptTemplate> CreateTemplateAsync(PromptTemplate template);

    /// <summary>
    /// Update an existing template
    /// </summary>
    Task UpdateTemplateAsync(PromptTemplate template);

    /// <summary>
    /// Delete a custom template (cannot delete built-in)
    /// </summary>
    Task<bool> DeleteTemplateAsync(int templateId);

    /// <summary>
    /// Reset to default built-in template for a category
    /// </summary>
    Task ResetToDefaultAsync(PromptCategory category);

    /// <summary>
    /// Get all built-in default templates
    /// </summary>
    Dictionary<PromptCategory, PromptTemplate> GetBuiltInTemplates();
}
