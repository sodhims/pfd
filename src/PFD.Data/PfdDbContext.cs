using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data;

public class PfdDbContext : DbContext
{
    public DbSet<DailyTask> DailyTasks { get; set; } = null!;
    public DbSet<Participant> Participants { get; set; } = null!;
    public DbSet<TaskParticipant> TaskParticipants { get; set; } = null!;

    public PfdDbContext(DbContextOptions<PfdDbContext> options) : base(options)
    {
    }

    public PfdDbContext()
    {
    }

    /// <summary>
    /// Ensures the database schema is up-to-date by adding any missing columns.
    /// Call this at application startup after EnsureCreated().
    /// </summary>
    public void EnsureSchemaUpdated()
    {
        var connection = Database.GetDbConnection();
        connection.Open();

        try
        {
            // Check and add missing columns to daily_tasks table
            var columnsToAdd = new Dictionary<string, string>
            {
                { "ScheduledTime", "TEXT NULL" },
                { "DurationMinutes", "INTEGER NOT NULL DEFAULT 30" },
                { "IsAllDay", "INTEGER NOT NULL DEFAULT 1" }
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

        // DailyTask configuration
        modelBuilder.Entity<DailyTask>(entity =>
        {
            entity.HasIndex(e => e.TaskDate);
            entity.HasIndex(e => e.IsCompleted);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("datetime('now')");
        });

        // Participant configuration
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasIndex(e => e.Name);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
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
    }
}
