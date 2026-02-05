using PFD.Shared.Interfaces;
using System.Collections.Concurrent;

namespace PFD.Services;

public class MicrosoftCalendarService : IExternalCalendarService
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tenantId;
    private readonly string _redirectUri;
    private readonly string[] _scopes = { "Calendars.Read", "User.Read" };

    // Store tokens in memory (in production, use database)
    private static readonly ConcurrentDictionary<int, MicrosoftTokenInfo> _userTokens = new();
    private static readonly ConcurrentDictionary<string, int> _pendingAuthStates = new();

    public CalendarSource Source => CalendarSource.Microsoft;
    public string DisplayName => "Microsoft (Teams/Outlook)";

    public MicrosoftCalendarService(string clientId, string clientSecret, string tenantId, string redirectUri)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tenantId = tenantId;
        _redirectUri = redirectUri;
    }

    public Task<string> GetAuthorizationUrlAsync(int userId)
    {
        var state = Guid.NewGuid().ToString();
        _pendingAuthStates[state] = userId;

        var scopeString = string.Join(" ", _scopes);
        var authUrl = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/authorize" +
            $"?client_id={Uri.EscapeDataString(_clientId)}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
            $"&response_mode=query" +
            $"&scope={Uri.EscapeDataString(scopeString + " offline_access")}" +
            $"&state={state}";

        return Task.FromResult(authUrl);
    }

    public async Task<bool> HandleAuthCallbackAsync(string code, int userId)
    {
        try
        {
            using var httpClient = new HttpClient();
            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["code"] = code,
                ["redirect_uri"] = _redirectUri,
                ["grant_type"] = "authorization_code",
                ["scope"] = string.Join(" ", _scopes) + " offline_access"
            });

            var response = await httpClient.PostAsync(tokenEndpoint, content);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<MicrosoftTokenResponse>(json);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
                return false;

            _userTokens[userId] = new MicrosoftTokenInfo
            {
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60)
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> IsConnectedAsync(int userId)
    {
        return Task.FromResult(_userTokens.ContainsKey(userId));
    }

    public Task DisconnectAsync(int userId)
    {
        _userTokens.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public async Task<List<ExternalCalendarEvent>> GetEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var events = new List<ExternalCalendarEvent>();

        if (!_userTokens.TryGetValue(userId, out var tokenInfo))
            return events;

        // Refresh token if needed
        if (DateTime.UtcNow >= tokenInfo.ExpiresAt)
        {
            var refreshed = await RefreshTokenAsync(userId, tokenInfo);
            if (!refreshed)
            {
                _userTokens.TryRemove(userId, out _);
                return events;
            }
            tokenInfo = _userTokens[userId];
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenInfo.AccessToken);

            var startStr = startDate.ToUniversalTime().ToString("o");
            var endStr = endDate.ToUniversalTime().ToString("o");

            var url = $"https://graph.microsoft.com/v1.0/me/calendarview" +
                $"?startDateTime={Uri.EscapeDataString(startStr)}" +
                $"&endDateTime={Uri.EscapeDataString(endStr)}" +
                $"&$select=id,subject,start,end,isAllDay,bodyPreview,location,organizer" +
                $"&$orderby=start/dateTime" +
                $"&$top=250";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _userTokens.TryRemove(userId, out _);
                return events;
            }

            var json = await response.Content.ReadAsStringAsync();
            var calendarResponse = System.Text.Json.JsonSerializer.Deserialize<GraphCalendarResponse>(json);

            if (calendarResponse?.value != null)
            {
                foreach (var item in calendarResponse.value)
                {
                    var evt = new ExternalCalendarEvent
                    {
                        ExternalId = item.id ?? "",
                        Source = CalendarSource.Microsoft,
                        Title = item.subject ?? "Untitled Event",
                        Description = item.bodyPreview,
                        Location = item.location?.displayName,
                        Organizer = item.organizer?.emailAddress?.name,
                        IsAllDay = item.isAllDay ?? false
                    };

                    // Parse dates
                    if (item.start != null && DateTime.TryParse(item.start.dateTime, out var startDt))
                    {
                        evt.Start = startDt;
                    }
                    if (item.end != null && DateTime.TryParse(item.end.dateTime, out var endDt))
                    {
                        evt.End = endDt;
                    }

                    events.Add(evt);
                }
            }
        }
        catch
        {
            _userTokens.TryRemove(userId, out _);
        }

        return events;
    }

    private async Task<bool> RefreshTokenAsync(int userId, MicrosoftTokenInfo tokenInfo)
    {
        if (string.IsNullOrEmpty(tokenInfo.RefreshToken))
            return false;

        try
        {
            using var httpClient = new HttpClient();
            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = tokenInfo.RefreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = string.Join(" ", _scopes) + " offline_access"
            });

            var response = await httpClient.PostAsync(tokenEndpoint, content);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<MicrosoftTokenResponse>(json);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
                return false;

            _userTokens[userId] = new MicrosoftTokenInfo
            {
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token ?? tokenInfo.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60)
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static int? GetUserIdFromState(string state)
    {
        if (_pendingAuthStates.TryRemove(state, out var userId))
            return userId;
        return null;
    }
}

internal class MicrosoftTokenInfo
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal class MicrosoftTokenResponse
{
    public string access_token { get; set; } = "";
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public string token_type { get; set; } = "";
}

internal class GraphCalendarResponse
{
    public List<GraphEvent>? value { get; set; }
}

internal class GraphEvent
{
    public string? id { get; set; }
    public string? subject { get; set; }
    public GraphDateTime? start { get; set; }
    public GraphDateTime? end { get; set; }
    public bool? isAllDay { get; set; }
    public string? bodyPreview { get; set; }
    public GraphLocation? location { get; set; }
    public GraphOrganizer? organizer { get; set; }
}

internal class GraphDateTime
{
    public string? dateTime { get; set; }
    public string? timeZone { get; set; }
}

internal class GraphLocation
{
    public string? displayName { get; set; }
}

internal class GraphOrganizer
{
    public GraphEmailAddress? emailAddress { get; set; }
}

internal class GraphEmailAddress
{
    public string? name { get; set; }
    public string? address { get; set; }
}
