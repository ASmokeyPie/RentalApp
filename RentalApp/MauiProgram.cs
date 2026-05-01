using Microsoft.Extensions.Logging;
using RentalApp.ViewModels;
using RentalApp.Database.Data;
using RentalApp.Database.Repositories;
using RentalApp.Database.Repositories.Api;
using RentalApp.Database.Repositories.Db;
using RentalApp.Views;
using System.Diagnostics;
using RentalApp.Services;
using RentalApp.Database.Services;

namespace RentalApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
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

            // The delegating handler attaches the stored token per request
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
            // AuthDelegatingHandler), so authenticated endpoints get the
            // token attached automatically.
            builder.Services.AddSingleton<ICategoryRepository, ApiCategoryRepository>();
            builder.Services.AddSingleton<IItemRepository, ApiItemRepository>();
            builder.Services.AddSingleton<IRentalRepository, ApiRentalRepository>();
            builder.Services.AddSingleton<IReviewRepository, ApiReviewRepository>();
        }
        else
        {
            // Local database context factory. Registered as a singleton so the same
            // factory instance (and underlying connection pool) is used across
            // the app. Repositories will create their own contexts from this factory as needed.
            builder.Services.AddDbContextFactory<AppDbContext>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<ICurrentUserProvider, CurrentUserProvider>();

            // Repositories — database-backed implementations. Each repository creates
            // its own DbContext from the factory as needed, so they can be
            // registered as singletons without sharing DbContext instances.
            builder.Services.AddSingleton<ICategoryRepository, DbCategoryRepository>();
            builder.Services.AddSingleton<IItemRepository, DbItemRepository>();
            builder.Services.AddSingleton<IRentalRepository, DbRentalRepository>();
            builder.Services.AddSingleton<IReviewRepository, DbReviewRepository>();
        }

        // Navigation service. Singleton, since it holds the global Shell
        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // GPS-backed location service. Singleton (stateless wrapper
        // around Geolocation.Default). Used by FindNearbyViewModel and the
        // "Use my location" button on CreateItemViewModel.
        builder.Services.AddSingleton<ILocationService, LocationService>();

        // Rental domain service. Sits over IRentalRepository, 
        // so the service is data-source-agnostic by construction.
        builder.Services.AddSingleton<IRentalService, RentalService>();

        // Review domain service. Same pattern — sits over the
        // review repo and adds the "borrower of a Completed rental" rules.
        builder.Services.AddSingleton<IReviewService, ReviewService>();

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

        // Item browsing (read paths). Both VMs are transient because
        // each navigation should start with a fresh state — list page resets
        // pagination on entry, detail page is loaded fresh per item id.
        builder.Services.AddTransient<ItemsListViewModel>();
        builder.Services.AddTransient<ItemsListPage>();
        builder.Services.AddTransient<ItemDetailsViewModel>();
        builder.Services.AddTransient<ItemDetailsPage>();

        // Item create + edit (write paths). Transient so each
        // navigation gets a fresh form state.
        builder.Services.AddTransient<CreateItemViewModel>();
        builder.Services.AddTransient<CreateItemPage>();
        builder.Services.AddTransient<EditItemViewModel>();
        builder.Services.AddTransient<EditItemPage>();

        // Rental request flow. Transient — the form should always
        // open with default dates and the right item id from the route.
        builder.Services.AddTransient<RequestRentalViewModel>();
        builder.Services.AddTransient<RequestRentalPage>();

        // User Rentals (incoming + outgoing tabbed list).
        builder.Services.AddTransient<MyRentalsViewModel>();
        builder.Services.AddTransient<MyRentalsPage>();

        // Rental detail + owner/borrower workflow actions.
        builder.Services.AddTransient<RentalDetailsViewModel>();
        builder.Services.AddTransient<RentalDetailsPage>();

        // Location-based discovery.
        builder.Services.AddTransient<FindNearbyViewModel>();
        builder.Services.AddTransient<FindNearbyPage>();

        // Review submission flow.
        builder.Services.AddTransient<WriteReviewViewModel>();
        builder.Services.AddTransient<WriteReviewPage>();

        // User profile — account info, average rating, reviews written.
        // Transient so each navigation gets a fresh load (reviews may change
        // between visits, e.g. after the user submits a new review).
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}