using Vigil.Application;
using Vigil.Application.Common.Interfaces;
using Vigil.MAUI.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Vigil.MAUI;

public static class MauiProgram
{
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

        // API base URL — localhost for dev, configurable for production
        var apiBase = "http://localhost:5300";
        builder.Services.AddHttpClient("AtlasAPI", client =>
        {
            client.BaseAddress = new Uri(apiBase);
        });

        // Register HTTP-based repositories
        builder.Services.AddScoped<ITaskRepository>(sp =>
            new HttpTaskRepository(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AtlasAPI")));
        builder.Services.AddScoped<IActivityRepository>(sp =>
            new HttpActivityRepository(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AtlasAPI")));
        builder.Services.AddScoped<ISecurityRepository>(sp =>
            new HttpSecurityRepository(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AtlasAPI")));
        builder.Services.AddScoped<IMonitoringRepository>(sp =>
            new HttpMonitoringRepository(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AtlasAPI")));
        builder.Services.AddSingleton<IDbConnectionFactory>(new HttpDbConnectionFactory());

        builder.Services.AddApplicationServices();
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
