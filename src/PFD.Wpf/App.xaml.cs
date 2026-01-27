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
        services.AddSingleton<IOllamaService>(sp => new OllamaService());
        services.AddSingleton<IAnalysisService>(sp => new AnalysisService());

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }
}
