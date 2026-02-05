using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;
using System.Collections.Concurrent;

namespace PFD.Services;

public class GoogleCalendarService : IGoogleCalendarService, IExternalCalendarService
{
    private readonly ITaskService _taskService;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    // Store tokens in memory (in production, use database)
    private static readonly ConcurrentDictionary<int, TokenResponse> _userTokens = new();

    // Store pending auth states
    private static readonly ConcurrentDictionary<string, int> _pendingAuthStates = new();

    public CalendarSource Source => CalendarSource.Google;
    public string DisplayName => "Google Calendar";

    public GoogleCalendarService(ITaskService taskService, string clientId, string clientSecret, string redirectUri)
    {
        _taskService = taskService;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _redirectUri = redirectUri;
    }

    public Task<string> GetAuthorizationUrlAsync(int userId)
    {
        var state = Guid.NewGuid().ToString();
        _pendingAuthStates[state] = userId;

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },
            Scopes = new[] { CalendarService.Scope.CalendarReadonly }
        });

        var authUri = flow.CreateAuthorizationCodeRequest(_redirectUri);
        authUri.State = state;

        return Task.FromResult(authUri.Build().ToString());
    }

    public async Task<bool> HandleAuthCallbackAsync(string code, int userId)
    {
        try
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _clientId,
                    ClientSecret = _clientSecret
                },
                Scopes = new[] { CalendarService.Scope.CalendarReadonly }
            });

            var token = await flow.ExchangeCodeForTokenAsync(
                userId.ToString(),
                code,
                _redirectUri,
                CancellationToken.None);

            _userTokens[userId] = token;
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

    public async Task<List<CalendarEventDto>> GetEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var externalEvents = await GetExternalEventsAsync(userId, startDate, endDate);
        return externalEvents.Select(e => new CalendarEventDto
        {
            Id = e.ExternalId,
            Title = e.Title,
            Start = e.Start,
            End = e.End,
            IsAllDay = e.IsAllDay,
            Description = e.Description,
            Location = e.Location
        }).ToList();
    }

    // IExternalCalendarService implementation
    async Task<List<ExternalCalendarEvent>> IExternalCalendarService.GetEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        return await GetExternalEventsAsync(userId, startDate, endDate);
    }

    private async Task<List<ExternalCalendarEvent>> GetExternalEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var service = await GetCalendarServiceAsync(userId);
        if (service == null)
            return new List<ExternalCalendarEvent>();

        var events = new List<ExternalCalendarEvent>();

        try
        {
            var request = service.Events.List("primary");
            request.TimeMinDateTimeOffset = new DateTimeOffset(startDate);
            request.TimeMaxDateTimeOffset = new DateTimeOffset(endDate);
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.MaxResults = 250;

            var response = await request.ExecuteAsync();

            if (response.Items != null)
            {
                foreach (var item in response.Items)
                {
                    var evt = new ExternalCalendarEvent
                    {
                        ExternalId = item.Id,
                        Source = CalendarSource.Google,
                        Title = item.Summary ?? "Untitled Event",
                        Description = item.Description,
                        Location = item.Location
                    };

                    // Handle all-day events
                    if (item.Start?.Date != null)
                    {
                        evt.IsAllDay = true;
                        evt.Start = DateTime.Parse(item.Start.Date);
                        evt.End = DateTime.Parse(item.End?.Date ?? item.Start.Date);
                    }
                    else if (item.Start?.DateTimeDateTimeOffset != null)
                    {
                        evt.IsAllDay = false;
                        evt.Start = item.Start.DateTimeDateTimeOffset.Value.LocalDateTime;
                        evt.End = item.End?.DateTimeDateTimeOffset?.LocalDateTime ?? evt.Start.AddHours(1);
                    }

                    events.Add(evt);
                }
            }
        }
        catch
        {
            // Token might be expired, remove it
            _userTokens.TryRemove(userId, out _);
        }

        return events;
    }

    public async Task<int> ImportEventsAsTasksAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var events = await GetEventsAsync(userId, startDate, endDate);
        var importedCount = 0;

        // Get existing tasks to avoid duplicates
        var existingTasks = await _taskService.GetTasksForDateRangeAsync(startDate, endDate, userId);
        var existingTitles = existingTasks
            .Select(t => $"{t.TaskDate:yyyy-MM-dd}|{t.Title}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var evt in events)
        {
            var taskDate = evt.Start.Date;
            var taskKey = $"{taskDate:yyyy-MM-dd}|{evt.Title}";

            // Skip if already exists
            if (existingTitles.Contains(taskKey))
                continue;

            var task = new DailyTask
            {
                Title = evt.Title,
                TaskDate = taskDate,
                IsAllDay = evt.IsAllDay,
                UserId = userId,
                IsCompleted = false,
                SortOrder = 999 // Add at end
            };

            // If timed event, set the scheduled time
            if (!evt.IsAllDay)
            {
                task.ScheduledTime = evt.Start.TimeOfDay;
                task.DurationMinutes = (int)(evt.End - evt.Start).TotalMinutes;
            }

            await _taskService.CreateTaskAsync(task);
            importedCount++;
        }

        return importedCount;
    }

    private async Task<CalendarService?> GetCalendarServiceAsync(int userId)
    {
        if (!_userTokens.TryGetValue(userId, out var token))
            return null;

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },
            Scopes = new[] { CalendarService.Scope.CalendarReadonly }
        });

        var credential = new UserCredential(flow, userId.ToString(), token);

        // Refresh token if needed
        if (credential.Token.IsStale)
        {
            try
            {
                await credential.RefreshTokenAsync(CancellationToken.None);
                _userTokens[userId] = credential.Token;
            }
            catch
            {
                _userTokens.TryRemove(userId, out _);
                return null;
            }
        }

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PFD Planner"
        });
    }

    public static int? GetUserIdFromState(string state)
    {
        if (_pendingAuthStates.TryRemove(state, out var userId))
            return userId;
        return null;
    }
}
