using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly PfdDbContext _context;

    public UserSettingsService(PfdDbContext context)
    {
        _context = context;
    }

    public async Task<UserSettings> GetSettingsAsync(string deviceId)
    {
        var settings = await _context.UserSettings
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId);

        if (settings == null)
        {
            settings = new UserSettings
            {
                DeviceId = deviceId,
                Theme = "teal",
                IsDailyView = true
            };
            _context.UserSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task SaveSettingsAsync(string deviceId, string theme, bool isDailyView, bool useLargeText)
    {
        var settings = await _context.UserSettings
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId);

        if (settings == null)
        {
            settings = new UserSettings
            {
                DeviceId = deviceId,
                Theme = theme,
                IsDailyView = isDailyView,
                UseLargeText = useLargeText
            };
            _context.UserSettings.Add(settings);
        }
        else
        {
            settings.Theme = theme;
            settings.IsDailyView = isDailyView;
            settings.UseLargeText = useLargeText;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string> GetThemeAsync(string deviceId)
    {
        var settings = await GetSettingsAsync(deviceId);
        return settings.Theme;
    }

    public async Task SetThemeAsync(string deviceId, string theme)
    {
        var settings = await _context.UserSettings
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId);

        if (settings == null)
        {
            settings = new UserSettings
            {
                DeviceId = deviceId,
                Theme = theme
            };
            _context.UserSettings.Add(settings);
        }
        else
        {
            settings.Theme = theme;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
