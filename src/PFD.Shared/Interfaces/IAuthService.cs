using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IAuthService
{
    Task<User?> LoginAsync(string username, string password);
    Task<User?> RegisterAsync(string username, string password, string? displayName = null);
    Task<bool> UsernameExistsAsync(string username);
    Task<User?> GetUserByIdAsync(int userId);
    Task UpdateUserSettingsAsync(int userId, string theme, bool isDailyView, bool useLargeText);
    Task UpdateUserProfileAsync(int userId, string? email, string? phone, string? address, string? displayName);
    Task UpdateLastLoginAsync(int userId);

    // Admin methods
    Task<List<User>> GetAllUsersAsync();
    Task<bool> ResetPasswordAsync(int userId, string newPassword);
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> IsAdminAsync(int userId);
    Task<AdminStats> GetAdminStatsAsync();
}

public class AdminStats
{
    public int TotalUsers { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int ActiveUsersLast7Days { get; set; }
    public Dictionary<string, int> ThemeUsage { get; set; } = new();
    public Dictionary<string, int> TasksPerUser { get; set; } = new();
    public long DatabaseSizeBytes { get; set; }
}
