using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data;

public class PfdDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<DailyTask> DailyTasks { get; set; } = null!;
    public DbSet<Participant> Participants { get; set; } = null!;
    public DbSet<TaskParticipant> TaskParticipants { get; set; } = null!;
    public DbSet<UserSettings> UserSettings { get; set; } = null!;
    public DbSet<CalendarCredentials> CalendarCredentials { get; set; } = null!;

    private bool IsSqlServer => Database.ProviderName?.Contains("SqlServer") == true;

    public PfdDbContext(DbContextOptions<PfdDbContext> options) : base(options)
    {
    }

    public PfdDbContext()
    {
    }

    /// <summary>
    /// Ensures the database schema is up-to-date.
    /// For SQLite, adds any missing columns and tables manually.
    /// For SQL Server / Azure SQL, EnsureCreated() handles everything.
    /// </summary>
    public void EnsureSchemaUpdated()
    {
        // For SQL Server, EnsureCreated() handles schema properly
        if (Database.ProviderName?.Contains("SqlServer") == true)
        {
            return; // Schema is managed by EF migrations or EnsureCreated
        }

        // SQLite manual schema updates
        var connection = Database.GetDbConnection();
        connection.Open();

        try
        {
            // Create users table if it doesn't exist
            try
            {
                using var checkUsersCmd = connection.CreateCommand();
                checkUsersCmd.CommandText = "SELECT Id FROM users LIMIT 1";
                checkUsersCmd.ExecuteScalar();
            }
            catch
            {
                // Users table doesn't exist, create it
                using var createUsersCmd = connection.CreateCommand();
                createUsersCmd.CommandText = @"
                    CREATE TABLE users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        DisplayName TEXT NULL,
                        Theme TEXT NOT NULL DEFAULT 'teal',
                        IsDailyView INTEGER NOT NULL DEFAULT 1,
                        UseLargeText INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        LastLoginAt TEXT NOT NULL DEFAULT (datetime('now'))
                    )";
                createUsersCmd.ExecuteNonQuery();
            }

            // Check and add missing columns to daily_tasks table
            var columnsToAdd = new Dictionary<string, string>
            {
                { "ScheduledTime", "TEXT NULL" },
                { "DurationMinutes", "INTEGER NOT NULL DEFAULT 30" },
                { "IsAllDay", "INTEGER NOT NULL DEFAULT 1" },
                { "UserId", "INTEGER NOT NULL DEFAULT 0" }
            };

            foreach (var column in columnsToAdd)
            {
                try
                {
                    using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = $"SELECT {column.Key} FROM daily_tasks LIMIT 1";
                    checkCmd.ExecuteScalar();
                }
                catch
                {
                    // Column doesn't exist, add it
                    using var addCmd = connection.CreateCommand();
                    addCmd.CommandText = $"ALTER TABLE daily_tasks ADD COLUMN {column.Key} {column.Value}";
                    addCmd.ExecuteNonQuery();
                }
            }

            // Create calendar_credentials table if it doesn't exist
            try
            {
                using var checkCredsCmd = connection.CreateCommand();
                checkCredsCmd.CommandText = "SELECT Id FROM calendar_credentials LIMIT 1";
                checkCredsCmd.ExecuteScalar();
            }
            catch
            {
                // Table doesn't exist, create it
                using var createCredsCmd = connection.CreateCommand();
                createCredsCmd.CommandText = @"
                    CREATE TABLE calendar_credentials (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        Provider TEXT NOT NULL,
                        ClientId TEXT NULL,
                        ClientSecret TEXT NULL,
                        TenantId TEXT NULL,
                        RedirectUri TEXT NULL,
                        AccessToken TEXT NULL,
                        RefreshToken TEXT NULL,
                        TokenExpiry TEXT NULL,
                        IsConnected INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        UNIQUE(UserId, Provider)
                    )";
                createCredsCmd.ExecuteNonQuery();
            }
        }
        finally
        {
            connection.Close();
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PFD");

            Directory.CreateDirectory(dbFolder);

            var dbPath = Path.Combine(dbFolder, "pfd.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Determine which SQL dialect to use
        var isSqlServer = Database.ProviderName?.Contains("SqlServer") == true;
        var defaultDateSql = isSqlServer ? "GETUTCDATE()" : "datetime('now')";

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.HasMany(u => u.Tasks)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DailyTask configuration
        modelBuilder.Entity<DailyTask>(entity =>
        {
            entity.HasIndex(e => e.TaskDate);
            entity.HasIndex(e => e.IsCompleted);
            entity.HasIndex(e => e.UserId);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql(defaultDateSql);
        });

        // Participant configuration
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasIndex(e => e.Name);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);
        });

        // TaskParticipant configuration (many-to-many)
        modelBuilder.Entity<TaskParticipant>(entity =>
        {
            entity.HasOne(tp => tp.Task)
                .WithMany(t => t.Participants)
                .HasForeignKey(tp => tp.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tp => tp.Participant)
                .WithMany(p => p.TaskParticipants)
                .HasForeignKey(tp => tp.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.TaskId, e.ParticipantId }).IsUnique();
        });

        // UserSettings configuration
        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasIndex(e => e.DeviceId).IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql(defaultDateSql);
        });

        // CalendarCredentials configuration
        modelBuilder.Entity<CalendarCredentials>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql(defaultDateSql);
        });
    }
}
