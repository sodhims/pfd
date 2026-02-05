namespace PFD.Shared.Interfaces;

public interface ICalendarCredentialsService
{
    Task<CalendarCredentialsDto?> GetCredentialsAsync(int userId, string provider);
    Task<List<CalendarCredentialsDto>> GetAllCredentialsAsync(int userId);
    Task SaveCredentialsAsync(int userId, CalendarCredentialsDto credentials);
    Task DeleteCredentialsAsync(int userId, string provider);
    Task UpdateTokensAsync(int userId, string provider, string accessToken, string? refreshToken, DateTime? expiry);
    Task SetConnectedAsync(int userId, string provider, bool isConnected);
}

public class CalendarCredentialsDto
{
    public string Provider { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }
    public string? RedirectUri { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public bool IsConnected { get; set; }
}
