using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class TwilioSmsService : ISmsNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;

    public TwilioSmsService(
        HttpClient? httpClient = null,
        string accountSid = "",
        string authToken = "",
        string fromNumber = "")
    {
        _httpClient = httpClient ?? new HttpClient();
        _accountSid = accountSid;
        _authToken = authToken;
        _fromNumber = fromNumber;
    }

    public async Task<bool> IsAvailableAsync()
    {
        return !string.IsNullOrEmpty(_accountSid) &&
               !string.IsNullOrEmpty(_authToken) &&
               !string.IsNullOrEmpty(_fromNumber);
    }

    public async Task<NotificationResult> SendNotificationAsync(Participant participant, NotificationMessage message)
    {
        var result = new NotificationResult
        {
            Channel = NotificationChannel.Sms,
            ParticipantId = participant.Id
        };

        if (string.IsNullOrEmpty(participant.Phone))
        {
            result.Success = false;
            result.ErrorMessage = "Participant has no phone number";
            return result;
        }

        if (!await IsAvailableAsync())
        {
            result.Success = false;
            result.ErrorMessage = "Twilio SMS service not configured";
            return result;
        }

        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json";

            // Format SMS body (plain text, max ~160 chars for single segment)
            var smsBody = FormatSmsBody(message);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("To", participant.Phone),
                new KeyValuePair<string, string>("From", _fromNumber),
                new KeyValuePair<string, string>("Body", smsBody)
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            var authBytes = Encoding.ASCII.GetBytes($"{_accountSid}:{_authToken}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var twilioResponse = JsonSerializer.Deserialize<TwilioResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                result.Success = true;
                result.MessageId = twilioResponse?.Sid;
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = $"Twilio error: {response.StatusCode} - {responseBody}";
            }
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
            if (participant.NotificationPreference.HasFlag(NotificationPreference.Sms))
            {
                var result = await SendNotificationAsync(participant, message);
                results.Add(result);
            }
        }

        return results;
    }

    private static string FormatSmsBody(NotificationMessage message)
    {
        // SMS should be concise
        var task = message.RelatedTask;
        if (task == null)
            return $"{message.Subject}\n{message.Body}".Substring(0, Math.Min(320, message.Body.Length));

        var dateStr = task.TaskDate.ToString("MMM d");
        var timeStr = task.ScheduledTime.HasValue
            ? task.ScheduledTime.Value.ToString(@"h\:mm tt")
            : "All day";

        return $"Meeting: {task.Title}\n{dateStr} @ {timeStr}\nFrom: {message.SenderName ?? "PFD Planner"}";
    }

    public static NotificationMessage CreateMeetingInviteSms(DailyTask task, string senderName)
    {
        var dateStr = task.TaskDate.ToString("MMM d");
        var timeStr = task.ScheduledTime.HasValue
            ? task.ScheduledTime.Value.ToString(@"h\:mm tt")
            : "All day";

        return new NotificationMessage
        {
            Type = NotificationType.MeetingInvite,
            Subject = "Meeting Invite",
            Body = $"Meeting: {task.Title}\n{dateStr} @ {timeStr}\nFrom: {senderName}",
            RelatedTask = task,
            SenderName = senderName
        };
    }

    private class TwilioResponse
    {
        public string? Sid { get; set; }
        public string? Status { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
