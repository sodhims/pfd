using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class SendGridNotificationService : IEmailNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _senderEmail;
    private readonly string _senderName;

    private const string SendGridApiUrl = "https://api.sendgrid.com/v3/mail/send";

    public SendGridNotificationService(
        HttpClient? httpClient = null,
        string apiKey = "",
        string senderEmail = "",
        string senderName = "PFD Planner")
    {
        _httpClient = httpClient ?? new HttpClient();
        _apiKey = apiKey;
        _senderEmail = senderEmail;
        _senderName = senderName;
    }

    public async Task<bool> IsAvailableAsync()
    {
        return !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_senderEmail);
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
            result.ErrorMessage = "SendGrid service not configured";
            return result;
        }

        try
        {
            var request = new SendGridRequest
            {
                Personalizations = new[]
                {
                    new SendGridPersonalization
                    {
                        To = new[] { new SendGridEmail { Email = participant.Email, Name = participant.Name } }
                    }
                },
                From = new SendGridEmail { Email = _senderEmail, Name = message.SenderName ?? _senderName },
                Subject = message.Subject,
                Content = new[]
                {
                    new SendGridContent
                    {
                        Type = string.IsNullOrEmpty(message.HtmlBody) ? "text/plain" : "text/html",
                        Value = message.HtmlBody ?? message.Body
                    }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SendGridApiUrl);
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await _httpClient.SendAsync(httpRequest);

            if (response.IsSuccessStatusCode)
            {
                result.Success = true;
                result.MessageId = response.Headers.TryGetValues("X-Message-Id", out var ids)
                    ? ids.FirstOrDefault()
                    : Guid.NewGuid().ToString();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                result.Success = false;
                result.ErrorMessage = $"SendGrid error: {response.StatusCode} - {error}";
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
            if (participant.NotificationPreference.HasFlag(NotificationPreference.Email))
            {
                var result = await SendNotificationAsync(participant, message);
                results.Add(result);
            }
        }

        return results;
    }

    // SendGrid API DTOs
    private class SendGridRequest
    {
        [JsonPropertyName("personalizations")]
        public SendGridPersonalization[] Personalizations { get; set; } = Array.Empty<SendGridPersonalization>();

        [JsonPropertyName("from")]
        public SendGridEmail From { get; set; } = new();

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonPropertyName("content")]
        public SendGridContent[] Content { get; set; } = Array.Empty<SendGridContent>();
    }

    private class SendGridPersonalization
    {
        [JsonPropertyName("to")]
        public SendGridEmail[] To { get; set; } = Array.Empty<SendGridEmail>();
    }

    private class SendGridEmail
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class SendGridContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text/plain";

        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }
}
