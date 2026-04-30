namespace RentalApp.Services;

/// <summary>
/// Thin abstraction over Shell navigation used by ViewModels.
/// Keeping navigation behind an interface makes it easier to unit test
/// ViewModels and to centralise route conventions.
/// </summary>
public interface INavigationService
{
    /// <summary>Navigate to the given Shell route.</summary>
    Task NavigateToAsync(string route);

    /// <summary>Navigate to the given route, passing query parameters.</summary>
    Task NavigateToAsync(string route, Dictionary<string, object> parameters);

    /// <summary>Navigate back one level.</summary>
    Task NavigateBackAsync();

    /// <summary>Navigate to the app's root route (login).</summary>
    Task NavigateToRootAsync();

    /// <summary>Pops the navigation stack to its root page.</summary>
    Task PopToRootAsync();
}