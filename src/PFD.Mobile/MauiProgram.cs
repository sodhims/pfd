using Microsoft.Extensions.Logging;
using PFD.Mobile.Services;
using PFD.Shared.Interfaces;

namespace PFD.Mobile;

public static class MauiProgram
{
    // Azure URL for the PFD Planner API
    public const string DefaultApiUrl = "https://pfd-planner.azurewebsites.net";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Use API service to connect to Azure server
        var apiService = new ApiService(DefaultApiUrl);
        builder.Services.AddSingleton<ITaskService>(apiService);
        builder.Services.AddSingleton<IAuthService>(apiService);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
