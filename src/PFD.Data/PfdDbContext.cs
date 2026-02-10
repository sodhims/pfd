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
    public DbSet<InsightHistory> InsightHistory { get; set; } = null!;
    public DbSet<PromptTemplate> PromptTemplates { get; set; } = null!;
    public DbSet<TaskTemplate> TaskTemplates { get; set; } = null!;
    public DbSet<SubtaskTemplate> SubtaskTemplates { get; set; } = null!;

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
                { "GroupId", "INT NULL" },
                { "RecurrenceType", "INT NOT NULL DEFAULT 0" },
                { "RecurrenceInterval", "INT NOT NULL DEFAULT 1" },
                { "RecurrenceDays", "NVARCHAR(50) NULL" },
                { "RecurrenceEndDate", "DATETIME2 NULL" },
                { "RecurrenceParentId", "INT NULL" }
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

            // Add NotificationPreference column to participants table
            try
            {
                using var addNotifPrefCmd = connection.CreateCommand();
                addNotifPrefCmd.CommandText = @"
                    IF NOT EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'participants' AND COLUMN_NAME = 'NotificationPreference'
                    )
                    BEGIN
                        ALTER TABLE participants ADD NotificationPreference INT NOT NULL DEFAULT 1;
                    END";
                addNotifPrefCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not add NotificationPreference column: {ex.Message}");
            }

            // Add Phone column to participants table
            try
            {
                using var addPhoneCmd = connection.CreateCommand();
                addPhoneCmd.CommandText = @"
                    IF NOT EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'participants' AND COLUMN_NAME = 'Phone'
                    )
                    BEGIN
                        ALTER TABLE participants ADD Phone NVARCHAR(50) NULL;
                    END";
                addPhoneCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not add Phone column to participants: {ex.Message}");
            }

            // Add personal info columns to users table
            var userColumns = new Dictionary<string, string>
            {
                { "Email", "NVARCHAR(200) NULL" },
                { "Phone", "NVARCHAR(50) NULL" },
                { "Address", "NVARCHAR(500) NULL" }
            };

            foreach (var column in userColumns)
            {
                try
                {
                    using var addUserColCmd = connection.CreateCommand();
                    addUserColCmd.CommandText = $@"
                        IF NOT EXISTS (
                            SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME = 'users' AND COLUMN_NAME = '{column.Key}'
                        )
                        BEGIN
                            ALTER TABLE users ADD {column.Key} {column.Value};
                        END";
                    addUserColCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not add {column.Key} column to users: {ex.Message}");
                }
            }

            // Create insight_history table if it doesn't exist
            try
            {
                using var createInsightCmd = connection.CreateCommand();
                createInsightCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'insight_history')
                    BEGIN
                        CREATE TABLE insight_history (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            UserId INT NOT NULL,
                            InsightType NVARCHAR(50) NOT NULL,
                            PeriodStart DATETIME2 NOT NULL,
                            PeriodEnd DATETIME2 NOT NULL,
                            RawDataSnapshot NVARCHAR(MAX) NOT NULL,
                            AiInsightText NVARCHAR(MAX) NULL,
                            AiSuggestions NVARCHAR(MAX) NULL,
                            PromptTemplateId INT NULL,
                            AiModel NVARCHAR(50) NULL,
                            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_insight_history_users FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_insight_history_user_type ON insight_history(UserId, InsightType);
                        CREATE INDEX IX_insight_history_created ON insight_history(CreatedAt);
                    END";
                createInsightCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create insight_history table: {ex.Message}");
            }

            // Create prompt_templates table if it doesn't exist
            try
            {
                using var createTemplateCmd = connection.CreateCommand();
                createTemplateCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'prompt_templates')
                    BEGIN
                        CREATE TABLE prompt_templates (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Name NVARCHAR(100) NOT NULL,
                            Category INT NOT NULL,
                            Description NVARCHAR(500) NULL,
                            SystemPrompt NVARCHAR(MAX) NOT NULL,
                            IsBuiltIn BIT NOT NULL DEFAULT 0,
                            UserId INT NULL,
                            IsActive BIT NOT NULL DEFAULT 0,
                            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE INDEX IX_prompt_templates_category ON prompt_templates(Category);
                        CREATE INDEX IX_prompt_templates_active ON prompt_templates(IsActive);
                    END";
                createTemplateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create prompt_templates table: {ex.Message}");
            }

            // Add UserId column to prompt_templates if missing
            try
            {
                using var addUserIdCmd = connection.CreateCommand();
                addUserIdCmd.CommandText = @"
                    IF NOT EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'prompt_templates' AND COLUMN_NAME = 'UserId'
                    )
                    BEGIN
                        ALTER TABLE prompt_templates ADD UserId INT NULL;
                    END";
                addUserIdCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not add UserId column to prompt_templates: {ex.Message}");
            }

            // Create task_templates table if it doesn't exist
            try
            {
                using var createTaskTemplatesCmd = connection.CreateCommand();
                createTaskTemplatesCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'task_templates')
                    BEGIN
                        CREATE TABLE task_templates (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            UserId INT NOT NULL,
                            Name NVARCHAR(200) NOT NULL,
                            Description NVARCHAR(500) NULL,
                            DefaultTaskType INT NOT NULL DEFAULT 0,
                            DefaultDurationMinutes INT NOT NULL DEFAULT 30,
                            DefaultIsAllDay BIT NOT NULL DEFAULT 1,
                            DefaultColor NVARCHAR(20) NULL,
                            SortOrder INT NOT NULL DEFAULT 0,
                            IsActive BIT NOT NULL DEFAULT 1,
                            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_task_templates_users FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_task_templates_user ON task_templates(UserId);
                    END";
                createTaskTemplatesCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create task_templates table: {ex.Message}");
            }

            // Create subtask_templates table if it doesn't exist
            try
            {
                using var createSubtaskTemplatesCmd = connection.CreateCommand();
                createSubtaskTemplatesCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'subtask_templates')
                    BEGIN
                        CREATE TABLE subtask_templates (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            TaskTemplateId INT NOT NULL,
                            Title NVARCHAR(300) NOT NULL,
                            Description NVARCHAR(500) NULL,
                            SortOrder INT NOT NULL DEFAULT 0,
                            DurationMinutes INT NULL,
                            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_subtask_templates_task FOREIGN KEY (TaskTemplateId) REFERENCES task_templates(Id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_subtask_templates_task ON subtask_templates(TaskTemplateId);
                    END";
                createSubtaskTemplatesCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create subtask_templates table: {ex.Message}");
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
                { "GroupId", "INTEGER NULL" },
                { "RecurrenceType", "INTEGER NOT NULL DEFAULT 0" },
                { "RecurrenceInterval", "INTEGER NOT NULL DEFAULT 1" },
                { "RecurrenceDays", "TEXT NULL" },
                { "RecurrenceEndDate", "TEXT NULL" },
                { "RecurrenceParentId", "INTEGER NULL" }
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

            // Add NotificationPreference column to participants table
            try
            {
                using var checkNotifCmd = connection.CreateCommand();
                checkNotifCmd.CommandText = "SELECT NotificationPreference FROM participants LIMIT 1";
                checkNotifCmd.ExecuteScalar();
            }
            catch
            {
                using var addNotifCmd = connection.CreateCommand();
                addNotifCmd.CommandText = "ALTER TABLE participants ADD COLUMN NotificationPreference INTEGER NOT NULL DEFAULT 1";
                addNotifCmd.ExecuteNonQuery();
            }

            // Add Phone column to participants table
            try
            {
                using var checkPhoneCmd = connection.CreateCommand();
                checkPhoneCmd.CommandText = "SELECT Phone FROM participants LIMIT 1";
                checkPhoneCmd.ExecuteScalar();
            }
            catch
            {
                using var addPhoneCmd = connection.CreateCommand();
                addPhoneCmd.CommandText = "ALTER TABLE participants ADD COLUMN Phone TEXT NULL";
                addPhoneCmd.ExecuteNonQuery();
            }

            // Add personal info columns to users table
            var userColumns = new[] { "Email", "Phone", "Address" };
            foreach (var column in userColumns)
            {
                try
                {
                    using var checkUserColCmd = connection.CreateCommand();
                    checkUserColCmd.CommandText = $"SELECT {column} FROM users LIMIT 1";
                    checkUserColCmd.ExecuteScalar();
                }
                catch
                {
                    using var addUserColCmd = connection.CreateCommand();
                    addUserColCmd.CommandText = $"ALTER TABLE users ADD COLUMN {column} TEXT NULL";
                    addUserColCmd.ExecuteNonQuery();
                }
            }

            // Create insight_history table if it doesn't exist
            try
            {
                using var checkInsightCmd = connection.CreateCommand();
                checkInsightCmd.CommandText = "SELECT Id FROM insight_history LIMIT 1";
                checkInsightCmd.ExecuteScalar();
            }
            catch
            {
                using var createInsightCmd = connection.CreateCommand();
                createInsightCmd.CommandText = @"
                    CREATE TABLE insight_history (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        InsightType TEXT NOT NULL,
                        PeriodStart TEXT NOT NULL,
                        PeriodEnd TEXT NOT NULL,
                        RawDataSnapshot TEXT NOT NULL,
                        AiInsightText TEXT NULL,
                        AiSuggestions TEXT NULL,
                        PromptTemplateId INTEGER NULL,
                        AiModel TEXT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
                    )";
                createInsightCmd.ExecuteNonQuery();

                using var createIndexCmd = connection.CreateCommand();
                createIndexCmd.CommandText = "CREATE INDEX IX_insight_history_user_type ON insight_history(UserId, InsightType)";
                createIndexCmd.ExecuteNonQuery();
            }

            // Create prompt_templates table if it doesn't exist
            try
            {
                using var checkTemplateCmd = connection.CreateCommand();
                checkTemplateCmd.CommandText = "SELECT Id FROM prompt_templates LIMIT 1";
                checkTemplateCmd.ExecuteScalar();
            }
            catch
            {
                using var createTemplateCmd = connection.CreateCommand();
                createTemplateCmd.CommandText = @"
                    CREATE TABLE prompt_templates (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Category INTEGER NOT NULL,
                        Description TEXT NULL,
                        SystemPrompt TEXT NOT NULL,
                        IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                    )";
                createTemplateCmd.ExecuteNonQuery();
            }

            // Create task_templates table if it doesn't exist
            try
            {
                using var checkTaskTemplatesCmd = connection.CreateCommand();
                checkTaskTemplatesCmd.CommandText = "SELECT Id FROM task_templates LIMIT 1";
                checkTaskTemplatesCmd.ExecuteScalar();
            }
            catch
            {
                using var createTaskTemplatesCmd = connection.CreateCommand();
                createTaskTemplatesCmd.CommandText = @"
                    CREATE TABLE task_templates (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        Name TEXT NOT NULL,
                        Description TEXT NULL,
                        DefaultTaskType INTEGER NOT NULL DEFAULT 0,
                        DefaultDurationMinutes INTEGER NOT NULL DEFAULT 30,
                        DefaultIsAllDay INTEGER NOT NULL DEFAULT 1,
                        DefaultColor TEXT NULL,
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
                    )";
                createTaskTemplatesCmd.ExecuteNonQuery();

                using var createTaskTemplatesIndexCmd = connection.CreateCommand();
                createTaskTemplatesIndexCmd.CommandText = "CREATE INDEX IX_task_templates_user ON task_templates(UserId)";
                createTaskTemplatesIndexCmd.ExecuteNonQuery();
            }

            // Create subtask_templates table if it doesn't exist
            try
            {
                using var checkSubtaskTemplatesCmd = connection.CreateCommand();
                checkSubtaskTemplatesCmd.CommandText = "SELECT Id FROM subtask_templates LIMIT 1";
                checkSubtaskTemplatesCmd.ExecuteScalar();
            }
            catch
            {
                using var createSubtaskTemplatesCmd = connection.CreateCommand();
                createSubtaskTemplatesCmd.CommandText = @"
                    CREATE TABLE subtask_templates (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TaskTemplateId INTEGER NOT NULL,
                        Title TEXT NOT NULL,
                        Description TEXT NULL,
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        DurationMinutes INTEGER NULL,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (TaskTemplateId) REFERENCES task_templates(Id) ON DELETE CASCADE
                    )";
                createSubtaskTemplatesCmd.ExecuteNonQuery();

                using var createSubtaskTemplatesIndexCmd = connection.CreateCommand();
                createSubtaskTemplatesIndexCmd.CommandText = "CREATE INDEX IX_subtask_templates_task ON subtask_templates(TaskTemplateId)";
                createSubtaskTemplatesIndexCmd.ExecuteNonQuery();
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

        // InsightHistory configuration
        modelBuilder.Entity<InsightHistory>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.InsightType });
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PromptTemplate configuration
        modelBuilder.Entity<PromptTemplate>(entity =>
        {
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql(defaultDateSql);
        });

        // TaskTemplate configuration
        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.HasIndex(e => e.UserId);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql(defaultDateSql);

            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.SubtaskTemplates)
                .WithOne(st => st.TaskTemplate)
                .HasForeignKey(st => st.TaskTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SubtaskTemplate configuration
        modelBuilder.Entity<SubtaskTemplate>(entity =>
        {
            entity.HasIndex(e => e.TaskTemplateId);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(defaultDateSql);
        });
    }
}
