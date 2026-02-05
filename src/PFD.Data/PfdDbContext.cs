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
    public DbSet<TaskGroup> TaskGroups { get; set; } = null!;
    public DbSet<GroupMember> GroupMembers { get; set; } = null!;

    private bool IsSqlServer => Database.ProviderName?.Contains("SqlServer") == true;

    public PfdDbContext(DbContextOptions<PfdDbContext> options) : base(options)
    {
    }

    public PfdDbContext()
    {
    }

    /// <summary>
    /// Ensures the database schema is up-to-date.
    /// Adds any missing columns manually for both SQLite and SQL Server.
    /// </summary>
    public void EnsureSchemaUpdated()
    {
        var isSqlServer = Database.ProviderName?.Contains("SqlServer") == true;

        if (isSqlServer)
        {
            EnsureSqlServerSchema();
        }
        else
        {
            EnsureSqliteSchema();
        }
    }

    private void EnsureSqlServerSchema()
    {
        var connection = Database.GetDbConnection();
        connection.Open();

        try
        {
            // Add missing columns to daily_tasks
            var columnsToAdd = new Dictionary<string, string>
            {
                { "ParentTaskId", "INT NULL" },
                { "GroupId", "INT NULL" }
            };

            foreach (var column in columnsToAdd)
            {
                try
                {
                    using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = $@"
                        IF NOT EXISTS (
                            SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME = 'daily_tasks' AND COLUMN_NAME = '{column.Key}'
                        )
                        BEGIN
                            ALTER TABLE daily_tasks ADD {column.Key} {column.Value};
                        END";
                    checkCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not add column {column.Key}: {ex.Message}");
                }
            }

            // Create task_groups table if it doesn't exist
            try
            {
                using var createGroupsCmd = connection.CreateCommand();
                createGroupsCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'task_groups')
                    BEGIN
                        CREATE TABLE task_groups (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Name NVARCHAR(100) NOT NULL,
                            LeaderUserId INT NOT NULL,
                            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_task_groups_users FOREIGN KEY (LeaderUserId) REFERENCES users(Id)
                        );
                    END";
                createGroupsCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create task_groups table: {ex.Message}");
            }

            // Create group_members table if it doesn't exist
            try
            {
                using var createMembersCmd = connection.CreateCommand();
                createMembersCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'group_members')
                    BEGIN
                        CREATE TABLE group_members (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            GroupId INT NOT NULL,
                            UserId INT NOT NULL,
                            JoinedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_group_members_groups FOREIGN KEY (GroupId) REFERENCES task_groups(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_group_members_users FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE,
                            CONSTRAINT UQ_group_members UNIQUE (GroupId, UserId)
                        );
                    END";
                createMembersCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create group_members table: {ex.Message}");
            }
        }
        finally
        {
            connection.Close();
        }
    }

    private void EnsureSqliteSchema()
    {
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
                { "UserId", "INTEGER NOT NULL DEFAULT 0" },
                { "ParentTaskId", "INTEGER NULL" },
                { "GroupId", "INTEGER NULL" }
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

            // Create task_groups table if it doesn't exist
            try
            {
                using var checkGroupsCmd = connection.CreateCommand();
                checkGroupsCmd.CommandText = "SELECT Id FROM task_groups LIMIT 1";
                checkGroupsCmd.ExecuteScalar();
            }
            catch
            {
                using var createGroupsCmd = connection.CreateCommand();
                createGroupsCmd.CommandText = @"
                    CREATE TABLE task_groups (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        LeaderUserId INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (LeaderUserId) REFERENCES users(Id)
                    )";
                createGroupsCmd.ExecuteNonQuery();
            }

            // Create group_members table if it doesn't exist
            try
            {
                using var checkMembersCmd = connection.CreateCommand();
                checkMembersCmd.CommandText = "SELECT Id FROM group_members LIMIT 1";
                checkMembersCmd.ExecuteScalar();
            }
            catch
            {
                using var createMembersCmd = connection.CreateCommand();
                createMembersCmd.CommandText = @"
                    CREATE TABLE group_members (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        GroupId INTEGER NOT NULL,
                        UserId INTEGER NOT NULL,
                        JoinedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (GroupId) REFERENCES task_groups(Id) ON DELETE CASCADE,
                        FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE,
                        UNIQUE(GroupId, UserId)
                    )";
                createMembersCmd.ExecuteNonQuery();
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
            entity.HasIndex(e => e.ParentTaskId);
            entity.HasIndex(e => e.GroupId);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql(defaultDateSql);

            // Self-referencing relationship for subtasks
            entity.HasOne(t => t.ParentTask)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // TaskGroup configuration
        modelBuilder.Entity<TaskGroup>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.HasOne(g => g.Leader)
                .WithMany(u => u.LedGroups)
                .HasForeignKey(g => g.LeaderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(g => g.SharedTasks)
                .WithOne(t => t.Group)
                .HasForeignKey(t => t.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // GroupMember configuration
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();

            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.HasOne(gm => gm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(gm => gm.User)
                .WithMany(u => u.GroupMemberships)
                .HasForeignKey(gm => gm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
