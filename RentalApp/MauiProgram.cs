using Microsoft.Extensions.Logging;
using RentalApp.ViewModels;
using RentalApp.Database.Data;
using RentalApp.Database.Repositories;
using RentalApp.Database.Repositories.Api;
using RentalApp.Database.Repositories.Db;
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

            // Repositories — API-backed implementations against the shared API.
            // Each takes the same singleton HttpClient (already wired with the
            // AuthDelegatingHandler), so authenticated endpoints get the Bearer
            // token attached automatically.
            builder.Services.AddSingleton<ICategoryRepository, ApiCategoryRepository>();
            builder.Services.AddSingleton<IItemRepository,     ApiItemRepository>();
            builder.Services.AddSingleton<IRentalRepository,   ApiRentalRepository>();
            builder.Services.AddSingleton<IReviewRepository,   ApiReviewRepository>();
        }
        else
        {
            builder.Services.AddDbContext<AppDbContext>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

            // Local-DB path: stub repositories. They throw NotImplementedException
            // until the EF-backed implementations land. Registered so DI still
            // resolves and the app starts; any code path that actually calls
            // them will fail loudly until the slice that fills them in.
            builder.Services.AddSingleton<ICategoryRepository, DbCategoryRepository>();
            builder.Services.AddSingleton<IItemRepository,     DbItemRepository>();
            builder.Services.AddSingleton<IRentalRepository,   DbRentalRepository>();
            builder.Services.AddSingleton<IReviewRepository,   DbReviewRepository>();
        }
        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // Phase 5a: rental domain service. Sits over IRentalRepository (which
        // is registered above against either Api* or Db* per useSharedApi),
        // so the service is data-source-agnostic by construction.
        builder.Services.AddSingleton<IRentalService, RentalService>();

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

        // Phase 3: Item browsing (read paths). Both VMs are transient because
        // each navigation should start with a fresh state — list page resets
        // pagination on entry, detail page is loaded fresh per item id.
        builder.Services.AddTransient<ItemsListViewModel>();
        builder.Services.AddTransient<ItemsListPage>();
        builder.Services.AddTransient<ItemDetailsViewModel>();
        builder.Services.AddTransient<ItemDetailsPage>();

        // Phase 4: Item create + edit (write paths). Transient so each
        // navigation gets a fresh form state.
        builder.Services.AddTransient<CreateItemViewModel>();
        builder.Services.AddTransient<CreateItemPage>();
        builder.Services.AddTransient<EditItemViewModel>();
        builder.Services.AddTransient<EditItemPage>();

        // Phase 5b: Rental request flow. Transient — the form should always
        // open with default dates and the right item id from the route.
        builder.Services.AddTransient<RequestRentalViewModel>();
        builder.Services.AddTransient<RequestRentalPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}