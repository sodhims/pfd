using System.Net;
using System.Net.Mail;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _senderEmail;
    private readonly string _senderPassword;
    private readonly string _senderName;
    private readonly bool _enableSsl;

    public EmailNotificationService(
        string smtpHost = "",
        int smtpPort = 587,
        string senderEmail = "",
        string senderPassword = "",
        string senderName = "PFD Planner",
        bool enableSsl = true)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _senderEmail = senderEmail;
        _senderPassword = senderPassword;
        _senderName = senderName;
        _enableSsl = enableSsl;
    }

    public async Task<bool> IsAvailableAsync()
    {
        return !string.IsNullOrEmpty(_smtpHost) &&
               !string.IsNullOrEmpty(_senderEmail) &&
               !string.IsNullOrEmpty(_senderPassword);
    }

    public async Task<NotificationResult> SendNotificationAsync(Participant participant, NotificationMessage message)
    {
        var result = new NotificationResult
        {
            Channel = NotificationChannel.Email,
            ParticipantId = participant.Id
        };

        if (string.IsNullOrEmpty(participant.Email))
        {
            result.Success = false;
            result.ErrorMessage = "Participant has no email address";
            return result;
        }

        if (!await IsAvailableAsync())
        {
            result.Success = false;
            result.ErrorMessage = "Email service not configured";
            return result;
        }

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_senderEmail, _senderPassword),
                EnableSsl = _enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_senderEmail, message.SenderName ?? _senderName),
                Subject = message.Subject,
                Body = message.HtmlBody ?? message.Body,
                IsBodyHtml = !string.IsNullOrEmpty(message.HtmlBody)
            };
            mailMessage.To.Add(new MailAddress(participant.Email, participant.Name));

            await client.SendMailAsync(mailMessage);

            result.Success = true;
            result.MessageId = Guid.NewGuid().ToString();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<List<NotificationResult>> SendBulkNotificationAsync(List<Participant> participants, NotificationMessage message)
    {
        var results = new List<NotificationResult>();

        foreach (var participant in participants)
        {
            if (participant.NotificationPreference.HasFlag(NotificationPreference.Email))
            {
                var result = await SendNotificationAsync(participant, message);
                results.Add(result);
            }
        }

        return results;
    }

    public static NotificationMessage CreateMeetingInvite(DailyTask task, string senderName)
    {
        var dateStr = task.TaskDate.ToString("dddd, MMMM d, yyyy");
        var timeStr = task.ScheduledTime.HasValue
            ? task.ScheduledTime.Value.ToString(@"h\:mm tt")
            : "All day";

        return new NotificationMessage
        {
            Type = NotificationType.MeetingInvite,
            Subject = $"Meeting Invite: {task.Title}",
            Body = $@"You've been invited to a meeting:

{task.Title}

Date: {dateStr}
Time: {timeStr}

{(task.Description != null ? $"Details: {task.Description}" : "")}

Sent by {senderName} via PFD Planner",
            HtmlBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2 style='color: #333;'>📅 Meeting Invite</h2>
    <div style='background: #f5f5f5; padding: 20px; border-radius: 8px;'>
        <h3 style='margin-top: 0;'>{task.Title}</h3>
        <p><strong>Date:</strong> {dateStr}</p>
        <p><strong>Time:</strong> {timeStr}</p>
        {(task.Description != null ? $"<p><strong>Details:</strong> {task.Description}</p>" : "")}
    </div>
    <p style='color: #666; font-size: 12px; margin-top: 20px;'>
        Sent by {senderName} via PFD Planner
    </p>
</div>",
            RelatedTask = task,
            SenderName = senderName
        };
    }
}
