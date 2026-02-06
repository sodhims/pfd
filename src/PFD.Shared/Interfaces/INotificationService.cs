using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Send a notification to a participant about a shared meeting/task
    /// </summary>
    Task<NotificationResult> SendNotificationAsync(Participant participant, NotificationMessage message);

    /// <summary>
    /// Send notifications to multiple participants
    /// </summary>
    Task<List<NotificationResult>> SendBulkNotificationAsync(List<Participant> participants, NotificationMessage message);

    /// <summary>
    /// Check if the notification service is configured and available
    /// </summary>
    Task<bool> IsAvailableAsync();
}

public interface IEmailNotificationService : INotificationService
{
}

public interface ISmsNotificationService : INotificationService
{
}

public class NotificationMessage
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public NotificationType Type { get; set; } = NotificationType.MeetingInvite;
    public DailyTask? RelatedTask { get; set; }
    public string? SenderName { get; set; }
}

public class NotificationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MessageId { get; set; }
    public NotificationChannel Channel { get; set; }
    public int ParticipantId { get; set; }
}

public enum NotificationType
{
    MeetingInvite,
    MeetingUpdate,
    MeetingCancellation,
    TaskAssignment,
    Reminder
}

public enum NotificationChannel
{
    Email,
    Sms,
    Push
}

public enum NotificationPreference
{
    None = 0,
    Email = 1,
    Sms = 2,
    Both = 3
}
