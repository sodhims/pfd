using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PFD.Data;
using PFD.Data.Repositories;
using PFD.Services;
using PFD.Shared.Interfaces;
using PFD.Wpf.ViewModels;

namespace PFD.Wpf;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Ensure database is created and schema is up-to-date
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PfdDbContext>();
        dbContext.Database.EnsureCreated();
        dbContext.EnsureSchemaUpdated();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddDbContext<PfdDbContext>(options =>
        {
            var dbFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PFD");
            Directory.CreateDirectory(dbFolder);
            var dbPath = Path.Combine(dbFolder, "pfd.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Repositories
        services.AddScoped<TaskRepository>();
        services.AddScoped<ParticipantRepository>();

        // Services
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IOllamaService>(sp => new OllamaService());
        services.AddSingleton<IAnalysisService>(sp => new AnalysisService());

        // External Calendar Services - configure with your credentials
        var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        var googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
        var googleRedirectUri = "http://localhost:5000/auth/google/callback";

        var msClientId = Environment.GetEnvironmentVariable("MS_CLIENT_ID");
        var msClientSecret = Environment.GetEnvironmentVariable("MS_CLIENT_SECRET");
        var msTenantId = Environment.GetEnvironmentVariable("MS_TENANT_ID") ?? "common";
        var msRedirectUri = "http://localhost:5000/auth/microsoft/callback";

        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
        {
            services.AddScoped<IGoogleCalendarService>(sp =>
            {
                var taskService = sp.GetRequiredService<ITaskService>();
                return new GoogleCalendarService(taskService, googleClientId, googleClientSecret, googleRedirectUri);
            });
        }

        if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
        {
            services.AddScoped<MicrosoftCalendarService>(sp =>
                new MicrosoftCalendarService(msClientId, msClientSecret, msTenantId, msRedirectUri));
        }

        // Register CalendarSyncService
        services.AddScoped<ICalendarSyncService>(sp =>
        {
            var taskService = sp.GetRequiredService<ITaskService>();
            var syncService = new CalendarSyncService(taskService);

            // Register available calendar services
            var googleSvc = sp.GetService<IGoogleCalendarService>();
            if (googleSvc is GoogleCalendarService gcs)
            {
                syncService.RegisterCalendarService(gcs);
            }

            var msSvc = sp.GetService<MicrosoftCalendarService>();
            if (msSvc != null)
            {
                syncService.RegisterCalendarService(msSvc);
            }

            return syncService;
        });

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }
}
