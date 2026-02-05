namespace PFD.Shared.Interfaces;

public interface IGoogleCalendarService
{
    Task<string> GetAuthorizationUrlAsync(int userId);
    Task<bool> HandleAuthCallbackAsync(string code, int userId);
    Task<bool> IsConnectedAsync(int userId);
    Task DisconnectAsync(int userId);
    Task<List<CalendarEventDto>> GetEventsAsync(int userId, DateTime startDate, DateTime endDate);
    Task<int> ImportEventsAsTasksAsync(int userId, DateTime startDate, DateTime endDate);
}

public class CalendarEventDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}
