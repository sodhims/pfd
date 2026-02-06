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
    // Azure SQL Database - use Transient lifetime to avoid concurrency issues in Blazor Server
    builder.Services.AddDbContext<PfdDbContext>(options =>
        options.UseSqlServer(azureSqlConnection, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        }), ServiceLifetime.Transient);
    Console.WriteLine("Using Azure SQL Database");
}
else
{
    // Local SQLite fallback - use Transient lifetime
    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PFD", "pfd.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    builder.Services.AddDbContext<PfdDbContext>(options => options.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Transient);
    Console.WriteLine($"Using local SQLite: {dbPath}");
}

// Repositories and Services - Use Transient for Blazor Server to avoid DbContext concurrency
builder.Services.AddTransient<TaskRepository>();
builder.Services.AddTransient<GroupRepository>();
builder.Services.AddTransient<ITaskService, TaskService>();
builder.Services.AddTransient<IGroupService, GroupService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddTransient<ICalendarCredentialsService, CalendarCredentialsService>();

// Prompt Templates and Insight History Services
builder.Services.AddTransient<IPromptTemplateService, PromptTemplateService>();
builder.Services.AddTransient<IInsightHistoryService, InsightHistoryService>();

// Analysis Service with history and templates
builder.Services.AddScoped<IAnalysisService>(sp =>
{
    var historyService = sp.GetService<IInsightHistoryService>();
    var templateService = sp.GetService<IPromptTemplateService>();
    return new AnalysisService(new HttpClient(), "http://localhost:11434", "mistral", historyService, templateService);
});

// Claude AI Service - set Claude:ApiKey in appsettings.json or CLAUDE_API_KEY env var
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? Environment.GetEnvironmentVariable("CLAUDE_API_KEY") ?? "";
var claudeModel = builder.Configuration["Claude:Model"] ?? "claude-sonnet-4-20250514";
builder.Services.AddScoped<IClaudeService>(sp =>
    new ClaudeService(new HttpClient(), claudeApiKey, claudeModel));
if (!string.IsNullOrEmpty(claudeApiKey))
    Console.WriteLine("Claude AI Service configured");
else
    Console.WriteLine("Claude AI not configured (set Claude:ApiKey or CLAUDE_API_KEY)");

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

// Notification Services
var smtpHost = builder.Configuration["Smtp:Host"] ?? "";
var smtpPort = int.TryParse(builder.Configuration["Smtp:Port"], out var port) ? port : 587;
var smtpEmail = builder.Configuration["Smtp:Email"] ?? "";
var smtpPassword = builder.Configuration["Smtp:Password"] ?? "";

var sendGridKey = builder.Configuration["SendGrid:ApiKey"] ?? Environment.GetEnvironmentVariable("SENDGRID_API_KEY") ?? "";
var sendGridEmail = builder.Configuration["SendGrid:SenderEmail"] ?? smtpEmail;

var twilioSid = builder.Configuration["Twilio:AccountSid"] ?? Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? "";
var twilioToken = builder.Configuration["Twilio:AuthToken"] ?? Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? "";
var twilioPhone = builder.Configuration["Twilio:FromNumber"] ?? Environment.GetEnvironmentVariable("TWILIO_FROM_NUMBER") ?? "";

// Register email service (prefer SendGrid if configured, fallback to SMTP)
if (!string.IsNullOrEmpty(sendGridKey))
{
    builder.Services.AddScoped<IEmailNotificationService>(sp =>
        new SendGridNotificationService(new HttpClient(), sendGridKey, sendGridEmail));
    Console.WriteLine("SendGrid email notifications configured");
}
else if (!string.IsNullOrEmpty(smtpHost) && !string.IsNullOrEmpty(smtpEmail))
{
    builder.Services.AddScoped<IEmailNotificationService>(sp =>
        new EmailNotificationService(smtpHost, smtpPort, smtpEmail, smtpPassword));
    Console.WriteLine("SMTP email notifications configured");
}

// Register SMS service
if (!string.IsNullOrEmpty(twilioSid) && !string.IsNullOrEmpty(twilioToken))
{
    builder.Services.AddScoped<ISmsNotificationService>(sp =>
        new TwilioSmsService(new HttpClient(), twilioSid, twilioToken, twilioPhone));
    Console.WriteLine("Twilio SMS notifications configured");
}

// Register unified notification service
builder.Services.AddScoped<INotificationService>(sp =>
{
    var emailService = sp.GetService<IEmailNotificationService>();
    var smsService = sp.GetService<ISmsNotificationService>();
    return new NotificationService(emailService, smsService);
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
