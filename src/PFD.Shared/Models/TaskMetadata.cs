namespace PFD.Shared.Models;

/// <summary>
/// AI-generated metadata for tasks (deserialized from JSON)
/// </summary>
public class TaskMetadata
{
    /// <summary>
    /// AI-determined category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Suggested student ID for academic tasks
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>
    /// AI-suggested due date
    /// </summary>
    public DateTime? SuggestedDueDate { get; set; }

    /// <summary>
    /// AI-suggested participant names for meetings
    /// </summary>
    public List<string>? SuggestedParticipants { get; set; }

    /// <summary>
    /// AI explanation of suggestions
    /// </summary>
    public string? AiNotes { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; }
}
