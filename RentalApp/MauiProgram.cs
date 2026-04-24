using Microsoft.Extensions.Logging;
using RentalApp.ViewModels;
using RentalApp.Database.Data;
using RentalApp.Views;
using System.Diagnostics;
using RentalApp.Services;

namespace RentalApp;

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
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        const bool useSharedApi = true; // Set to false to use local database and authentication

        if (useSharedApi)
        {
            // Token storage (SecureStorage on device).
            builder.Services.AddSingleton<ITokenStorage, SecureStorageTokenStorage>();

            // The delegating handler attaches the stored Bearer token per request
            // and raises AuthenticationExpired on 401s to authenticated requests.
            // It is a singleton so ApiAuthenticationService can subscribe to its
            // event exactly once.
            builder.Services.AddSingleton(sp => new AuthDelegatingHandler(
                sp.GetRequiredService<ITokenStorage>())
            {
                InnerHandler = new HttpClientHandler(),
            });

            builder.Services.AddSingleton(sp => new HttpClient(
                sp.GetRequiredService<AuthDelegatingHandler>(), disposeHandler: false)
            {
                BaseAddress = new Uri("https://set09102-api.b-davison.workers.dev/"),
            });

            builder.Services.AddSingleton<IAuthenticationService, ApiAuthenticationService>();
        }
        else
        {
            builder.Services.AddDbContext<AppDbContext>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        }
        builder.Services.AddSingleton<INavigationService, NavigationService>();

        builder.Services.AddSingleton<AppShellViewModel>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<App>();

        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<RegisterViewModel>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddSingleton<TempViewModel>();
        builder.Services.AddTransient<TempPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}