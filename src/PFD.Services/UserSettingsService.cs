using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly IDbContextFactory<PfdDbContext> _contextFactory;

    public UserSettingsService(IDbContextFactory<PfdDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<UserSettings> GetSettingsAsync(string deviceId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var settings = await context.UserSettings
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId);

        if (settings == null)
        {
            settings = new UserSettings
            {
                DeviceId = deviceId,
                Theme = "teal",
                IsDailyView = true
            };
            context.UserSettings.Add(settings);
            await context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task SaveSettingsAsync(string deviceId, string theme, bool isDailyView, bool useLargeText)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var settings = await context.UserSettings
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
            context.UserSettings.Add(settings);
        }
        else
        {
            settings.Theme = theme;
            settings.IsDailyView = isDailyView;
            settings.UseLargeText = useLargeText;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task<string> GetThemeAsync(string deviceId)
    {
        var settings = await GetSettingsAsync(deviceId);
        return settings.Theme;
    }

    public async Task SetThemeAsync(string deviceId, string theme)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var settings = await context.UserSettings
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId);

        if (settings == null)
        {
            settings = new UserSettings
            {
                DeviceId = deviceId,
                Theme = theme
            };
            context.UserSettings.Add(settings);
        }
        else
        {
            settings.Theme = theme;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }
}
