using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class CalendarSyncService : ICalendarSyncService
{
    private readonly ITaskService _taskService;
    private readonly Dictionary<CalendarSource, IExternalCalendarService> _calendarServices;

    public CalendarSyncService(ITaskService taskService, IEnumerable<IExternalCalendarService>? calendarServices = null)
    {
        _taskService = taskService;
        _calendarServices = new Dictionary<CalendarSource, IExternalCalendarService>();

        if (calendarServices != null)
        {
            foreach (var service in calendarServices)
            {
                _calendarServices[service.Source] = service;
            }
        }
    }

    public void RegisterCalendarService(IExternalCalendarService service)
    {
        _calendarServices[service.Source] = service;
    }

    public IExternalCalendarService? GetCalendarService(CalendarSource source)
    {
        return _calendarServices.TryGetValue(source, out var service) ? service : null;
    }

    public async Task<List<CalendarSource>> GetConnectedCalendarsAsync(int userId)
    {
        var connected = new List<CalendarSource> { CalendarSource.Integrated }; // Always have integrated

        foreach (var (source, service) in _calendarServices)
        {
            if (await service.IsConnectedAsync(userId))
            {
                connected.Add(source);
            }
        }

        return connected;
    }

    public async Task<List<ExternalCalendarEvent>> GetAllEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var allEvents = new List<ExternalCalendarEvent>();

        // Get integrated tasks as events
        var integratedEvents = await GetIntegratedEventsAsync(userId, startDate, endDate);
        allEvents.AddRange(integratedEvents);

        // Get events from all connected external calendars
        foreach (var (source, service) in _calendarServices)
        {
            if (await service.IsConnectedAsync(userId))
            {
                try
                {
                    var events = await service.GetEventsAsync(userId, startDate, endDate);

                    // Mark which events are already imported
                    await MarkImportedEventsAsync(userId, events, startDate, endDate);

                    allEvents.AddRange(events);
                }
                catch
                {
                    // Continue with other calendars if one fails
                }
            }
        }

        return allEvents.OrderBy(e => e.Start).ToList();
    }

    public async Task<List<ExternalCalendarEvent>> GetEventsBySourceAsync(int userId, CalendarSource source, DateTime startDate, DateTime endDate)
    {
        if (source == CalendarSource.Integrated)
        {
            return await GetIntegratedEventsAsync(userId, startDate, endDate);
        }

        if (_calendarServices.TryGetValue(source, out var service) && await service.IsConnectedAsync(userId))
        {
            var events = await service.GetEventsAsync(userId, startDate, endDate);
            await MarkImportedEventsAsync(userId, events, startDate, endDate);
            return events;
        }

        return new List<ExternalCalendarEvent>();
    }

    private async Task<List<ExternalCalendarEvent>> GetIntegratedEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var tasks = await _taskService.GetTasksForDateRangeAsync(startDate, endDate, userId);

        return tasks.Select(t => new ExternalCalendarEvent
        {
            ExternalId = t.Id.ToString(),
            Source = CalendarSource.Integrated,
            Title = t.Title,
            Start = t.ScheduledTime.HasValue
                ? t.TaskDate.Add(t.ScheduledTime.Value)
                : t.TaskDate,
            End = t.ScheduledTime.HasValue
                ? t.TaskDate.Add(t.ScheduledTime.Value).AddMinutes(t.DurationMinutes)
                : t.TaskDate.AddDays(1),
            IsAllDay = t.IsAllDay,
            IsImported = true // Integrated events are "already imported"
        }).ToList();
    }

    private async Task MarkImportedEventsAsync(int userId, List<ExternalCalendarEvent> events, DateTime startDate, DateTime endDate)
    {
        // Get existing tasks to check which events are already imported
        var existingTasks = await _taskService.GetTasksForDateRangeAsync(startDate, endDate, userId);
        var existingTitles = existingTasks
            .Select(t => $"{t.TaskDate:yyyy-MM-dd}|{t.Title.ToLowerInvariant()}")
            .ToHashSet();

        foreach (var evt in events)
        {
            var taskKey = $"{evt.Start.Date:yyyy-MM-dd}|{evt.Title.ToLowerInvariant()}";
            evt.IsImported = existingTitles.Contains(taskKey);
        }
    }

    public async Task<int> ImportEventsToIntegratedAsync(int userId, List<ExternalCalendarEvent> events)
    {
        var importedCount = 0;

        foreach (var evt in events)
        {
            if (!evt.IsImported && evt.Source != CalendarSource.Integrated)
            {
                var count = await ImportEventToIntegratedAsync(userId, evt);
                importedCount += count;
            }
        }

        return importedCount;
    }

    public async Task<int> ImportEventToIntegratedAsync(int userId, ExternalCalendarEvent evt)
    {
        if (evt.IsImported || evt.Source == CalendarSource.Integrated)
            return 0;

        // Check if already exists
        var existingTasks = await _taskService.GetTasksForDateAsync(evt.Start.Date, userId);
        var alreadyExists = existingTasks.Any(t =>
            t.Title.Equals(evt.Title, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
            return 0;

        var task = new DailyTask
        {
            Title = evt.Title,
            TaskDate = evt.Start.Date,
            IsAllDay = evt.IsAllDay,
            UserId = userId,
            IsCompleted = false,
            SortOrder = 999
        };

        if (!evt.IsAllDay)
        {
            task.ScheduledTime = evt.Start.TimeOfDay;
            task.DurationMinutes = (int)(evt.End - evt.Start).TotalMinutes;
            if (task.DurationMinutes <= 0) task.DurationMinutes = 30;
        }

        await _taskService.CreateTaskAsync(task);
        evt.IsImported = true;
        return 1;
    }
}
