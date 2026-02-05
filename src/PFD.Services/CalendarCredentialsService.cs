using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class CalendarCredentialsService : ICalendarCredentialsService
{
    private readonly PfdDbContext _context;

    public CalendarCredentialsService(PfdDbContext context)
    {
        _context = context;
    }

    public async Task<CalendarCredentialsDto?> GetCredentialsAsync(int userId, string provider)
    {
        var creds = await _context.CalendarCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

        if (creds == null) return null;

        return MapToDto(creds);
    }

    public async Task<List<CalendarCredentialsDto>> GetAllCredentialsAsync(int userId)
    {
        var creds = await _context.CalendarCredentials
            .Where(c => c.UserId == userId)
            .ToListAsync();

        return creds.Select(MapToDto).ToList();
    }

    public async Task SaveCredentialsAsync(int userId, CalendarCredentialsDto dto)
    {
        var existing = await _context.CalendarCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == dto.Provider);

        if (existing != null)
        {
            existing.ClientId = dto.ClientId;
            existing.ClientSecret = dto.ClientSecret;
            existing.TenantId = dto.TenantId;
            existing.RedirectUri = dto.RedirectUri;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var creds = new CalendarCredentials
            {
                UserId = userId,
                Provider = dto.Provider,
                ClientId = dto.ClientId,
                ClientSecret = dto.ClientSecret,
                TenantId = dto.TenantId,
                RedirectUri = dto.RedirectUri,
                IsConnected = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CalendarCredentials.Add(creds);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteCredentialsAsync(int userId, string provider)
    {
        var creds = await _context.CalendarCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

        if (creds != null)
        {
            _context.CalendarCredentials.Remove(creds);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateTokensAsync(int userId, string provider, string accessToken, string? refreshToken, DateTime? expiry)
    {
        var creds = await _context.CalendarCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

        if (creds != null)
        {
            creds.AccessToken = accessToken;
            if (refreshToken != null)
                creds.RefreshToken = refreshToken;
            creds.TokenExpiry = expiry;
            creds.IsConnected = true;
            creds.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetConnectedAsync(int userId, string provider, bool isConnected)
    {
        var creds = await _context.CalendarCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

        if (creds != null)
        {
            creds.IsConnected = isConnected;
            if (!isConnected)
            {
                creds.AccessToken = null;
                creds.RefreshToken = null;
                creds.TokenExpiry = null;
            }
            creds.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private static CalendarCredentialsDto MapToDto(CalendarCredentials creds)
    {
        return new CalendarCredentialsDto
        {
            Provider = creds.Provider,
            ClientId = creds.ClientId,
            ClientSecret = creds.ClientSecret,
            TenantId = creds.TenantId,
            RedirectUri = creds.RedirectUri,
            AccessToken = creds.AccessToken,
            RefreshToken = creds.RefreshToken,
            TokenExpiry = creds.TokenExpiry,
            IsConnected = creds.IsConnected
        };
    }
}
