using Microsoft.EntityFrameworkCore;
using PFD.Blazor.Components;
using PFD.Data;
using PFD.Data.Repositories;
using PFD.Services;
using PFD.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API controllers
builder.Services.AddControllers();

// Database - Use Azure SQL if connection string provided, otherwise local SQLite
var azureSqlConnection = builder.Configuration.GetConnectionString("AzureSql");
if (!string.IsNullOrEmpty(azureSqlConnection))
{
    // Azure SQL Database
    builder.Services.AddDbContext<PfdDbContext>(options =>
        options.UseSqlServer(azureSqlConnection));
    Console.WriteLine("Using Azure SQL Database");
}
else
{
    // Local SQLite fallback
    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PFD", "pfd.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    builder.Services.AddDbContext<PfdDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
    Console.WriteLine($"Using local SQLite: {dbPath}");
}

// Repositories and Services
builder.Services.AddScoped<TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICalendarCredentialsService, CalendarCredentialsService>();

// External Calendar Services - configure with your credentials
var googleClientId = builder.Configuration["Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = builder.Configuration["Google:ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
var googleRedirectUri = builder.Configuration["Google:RedirectUri"] ?? "https://localhost:5001/auth/google/callback";

var msClientId = builder.Configuration["Microsoft:ClientId"] ?? Environment.GetEnvironmentVariable("MS_CLIENT_ID");
var msClientSecret = builder.Configuration["Microsoft:ClientSecret"] ?? Environment.GetEnvironmentVariable("MS_CLIENT_SECRET");
var msTenantId = builder.Configuration["Microsoft:TenantId"] ?? Environment.GetEnvironmentVariable("MS_TENANT_ID") ?? "common";
var msRedirectUri = builder.Configuration["Microsoft:RedirectUri"] ?? "https://localhost:5001/auth/microsoft/callback";

// Register individual calendar services
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddScoped<IGoogleCalendarService>(sp =>
    {
        var taskService = sp.GetRequiredService<ITaskService>();
        return new GoogleCalendarService(taskService, googleClientId, googleClientSecret, googleRedirectUri);
    });

    builder.Services.AddScoped<IExternalCalendarService>(sp =>
    {
        var taskService = sp.GetRequiredService<ITaskService>();
        return new GoogleCalendarService(taskService, googleClientId, googleClientSecret, googleRedirectUri);
    });
}

if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
{
    builder.Services.AddScoped<MicrosoftCalendarService>(sp =>
        new MicrosoftCalendarService(msClientId, msClientSecret, msTenantId, msRedirectUri));
}

// Register CalendarSyncService
builder.Services.AddScoped<ICalendarSyncService>(sp =>
{
    var taskService = sp.GetRequiredService<ITaskService>();
    var calendarServices = sp.GetServices<IExternalCalendarService>();
    var syncService = new CalendarSyncService(taskService, calendarServices);

    // Also register Microsoft service if available
    var msService = sp.GetService<MicrosoftCalendarService>();
    if (msService != null)
    {
        syncService.RegisterCalendarService(msService);
    }

    return syncService;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

// Map API controllers
app.MapControllers();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure database is created and schema is up-to-date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PfdDbContext>();
    db.Database.EnsureCreated();
    db.EnsureSchemaUpdated();
}

app.Run();
