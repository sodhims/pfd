namespace PFD.Shared.Interfaces;

public enum CalendarSource
{
    Integrated,  // Local PFD tasks
    Google,
    Microsoft,   // Teams/Outlook
    Apple        // Future support
}

public interface IExternalCalendarService
{
    CalendarSource Source { get; }
    string DisplayName { get; }
    Task<string> GetAuthorizationUrlAsync(int userId);
    Task<bool> HandleAuthCallbackAsync(string code, int userId);
    Task<bool> IsConnectedAsync(int userId);
    Task DisconnectAsync(int userId);
    Task<List<ExternalCalendarEvent>> GetEventsAsync(int userId, DateTime startDate, DateTime endDate);
}

public class ExternalCalendarEvent
{
    public string ExternalId { get; set; } = "";
    public CalendarSource Source { get; set; }
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Organizer { get; set; }
    public bool IsImported { get; set; } = false; // Track if already imported to Integrated
}

public interface ICalendarSyncService
{
    Task<List<CalendarSource>> GetConnectedCalendarsAsync(int userId);
    Task<List<ExternalCalendarEvent>> GetAllEventsAsync(int userId, DateTime startDate, DateTime endDate);
    Task<List<ExternalCalendarEvent>> GetEventsBySourceAsync(int userId, CalendarSource source, DateTime startDate, DateTime endDate);
    Task<int> ImportEventsToIntegratedAsync(int userId, List<ExternalCalendarEvent> events);
    Task<int> ImportEventToIntegratedAsync(int userId, ExternalCalendarEvent evt);
    IExternalCalendarService? GetCalendarService(CalendarSource source);
}
