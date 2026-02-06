using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

/// <summary>
/// Unified notification service that routes to the appropriate channel
/// based on participant preferences.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IEmailNotificationService? _emailService;
    private readonly ISmsNotificationService? _smsService;

    public NotificationService(
        IEmailNotificationService? emailService = null,
        ISmsNotificationService? smsService = null)
    {
        _emailService = emailService;
        _smsService = smsService;
    }

    public async Task<bool> IsAvailableAsync()
    {
        var emailAvailable = _emailService != null && await _emailService.IsAvailableAsync();
        var smsAvailable = _smsService != null && await _smsService.IsAvailableAsync();
        return emailAvailable || smsAvailable;
    }

    public async Task<NotificationResult> SendNotificationAsync(Participant participant, NotificationMessage message)
    {
        var results = new List<NotificationResult>();

        // Send via preferred channels
        if (participant.NotificationPreference.HasFlag(NotificationPreference.Email) &&
            !string.IsNullOrEmpty(participant.Email) &&
            _emailService != null &&
            await _emailService.IsAvailableAsync())
        {
            var emailResult = await _emailService.SendNotificationAsync(participant, message);
            results.Add(emailResult);
        }

        if (participant.NotificationPreference.HasFlag(NotificationPreference.Sms) &&
            !string.IsNullOrEmpty(participant.Phone) &&
            _smsService != null &&
            await _smsService.IsAvailableAsync())
        {
            var smsResult = await _smsService.SendNotificationAsync(participant, message);
            results.Add(smsResult);
        }

        // Return combined result
        if (!results.Any())
        {
            return new NotificationResult
            {
                Success = false,
                ErrorMessage = "No notification channel available for this participant",
                ParticipantId = participant.Id
            };
        }

        // If any succeeded, consider it a success
        var anySuccess = results.Any(r => r.Success);
        return new NotificationResult
        {
            Success = anySuccess,
            ErrorMessage = anySuccess ? null : string.Join("; ", results.Where(r => !r.Success).Select(r => r.ErrorMessage)),
            ParticipantId = participant.Id,
            Channel = results.FirstOrDefault(r => r.Success)?.Channel ?? NotificationChannel.Email
        };
    }

    public async Task<List<NotificationResult>> SendBulkNotificationAsync(List<Participant> participants, NotificationMessage message)
    {
        var results = new List<NotificationResult>();

        foreach (var participant in participants)
        {
            if (participant.NotificationPreference != NotificationPreference.None)
            {
                var result = await SendNotificationAsync(participant, message);
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Send meeting invites to all participants of a task
    /// </summary>
    public async Task<List<NotificationResult>> SendMeetingInvitesAsync(
        DailyTask task,
        List<Participant> participants,
        string senderName)
    {
        var emailMessage = EmailNotificationService.CreateMeetingInvite(task, senderName);
        return await SendBulkNotificationAsync(participants, emailMessage);
    }
}
