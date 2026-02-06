using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IOllamaService
{
    Task<TaskMetadata?> AugmentTaskAsync(string taskText, List<Participant>? recentParticipants = null);
    Task<bool> IsAvailableAsync();
    Task<TaskAction?> ParseTaskActionAsync(string taskText);

    /// <summary>
    /// Deep calendar pattern analysis - examines priorities, workload, routines, and coordination
    /// </summary>
    Task<CalendarAnalysis> AnalyzeCalendarPatternsAsync(List<DailyTask> tasks, int daysToAnalyze = 30);
}

public class TaskAction
{
    public TaskActionType ActionType { get; set; }
    public string? Target { get; set; }      // Email address, phone number, file path
    public string? Subject { get; set; }     // Email subject or call purpose
    public string? Body { get; set; }        // Email body draft
    public string? SearchQuery { get; set; } // For looking up contacts
    public double Confidence { get; set; }
}

public enum TaskActionType
{
    None,
    Email,
    Call,
    Document,
    Website,
    Meeting
}
