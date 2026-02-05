using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IUserSettingsService
{
    Task<UserSettings> GetSettingsAsync(string deviceId);
    Task SaveSettingsAsync(string deviceId, string theme, bool isDailyView, bool useLargeText);
    Task<string> GetThemeAsync(string deviceId);
    Task SetThemeAsync(string deviceId, string theme);
}
