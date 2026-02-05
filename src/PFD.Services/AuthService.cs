using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PFD.Data;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class AuthService : IAuthService
{
    private readonly PfdDbContext _context;

    public AuthService(PfdDbContext context)
    {
        _context = context;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null)
            return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> RegisterAsync(string username, string password, string? displayName = null)
    {
        if (await UsernameExistsAsync(username))
            return null;

        var user = new User
        {
            Username = username.ToLower(),
            PasswordHash = HashPassword(password),
            DisplayName = displayName ?? username,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task UpdateUserSettingsAsync(int userId, string theme, bool isDailyView, bool useLargeText)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.Theme = theme;
            user.IsDailyView = isDailyView;
            user.UseLargeText = useLargeText;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Id)
            .ToListAsync();
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsAdminAsync(int userId)
    {
        // User with Id=1 is the superuser/admin
        return Task.FromResult(userId == 1);
    }

    public async Task<AdminStats> GetAdminStatsAsync()
    {
        var stats = new AdminStats();

        // User counts
        var users = await _context.Users.ToListAsync();
        stats.TotalUsers = users.Count;
        stats.ActiveUsersLast7Days = users.Count(u => u.LastLoginAt >= DateTime.UtcNow.AddDays(-7));

        // Theme usage
        stats.ThemeUsage = users
            .GroupBy(u => u.Theme ?? "default")
            .ToDictionary(g => g.Key, g => g.Count());

        // Task counts
        var tasks = await _context.DailyTasks.ToListAsync();
        stats.TotalTasks = tasks.Count;
        stats.CompletedTasks = tasks.Count(t => t.IsCompleted);

        // Tasks per user
        var tasksPerUser = tasks
            .GroupBy(t => t.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        stats.TasksPerUser = new Dictionary<string, int>();
        foreach (var user in users)
        {
            var taskCount = tasksPerUser.GetValueOrDefault(user.Id, 0);
            stats.TasksPerUser[user.Username] = taskCount;
        }

        // Database size (for SQLite)
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PFD", "pfd.db");
            if (File.Exists(dbPath))
            {
                stats.DatabaseSizeBytes = new FileInfo(dbPath).Length;
            }
        }
        catch
        {
            stats.DatabaseSizeBytes = 0;
        }

        return stats;
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
