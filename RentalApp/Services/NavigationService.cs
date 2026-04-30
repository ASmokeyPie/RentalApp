namespace RentalApp.Services;

/// <summary>
/// Default <see cref="INavigationService"/> implementation backed by
/// <see cref="Shell.Current"/>.
/// </summary>
public class NavigationService : INavigationService
{
    /// <inheritdoc />
    public async Task NavigateToAsync(string route)
    {
        await Shell.Current.GoToAsync(route);
    }

    /// <inheritdoc />
    public async Task NavigateToAsync(string route, Dictionary<string, object> parameters)
    {
        await Shell.Current.GoToAsync(route, parameters);
    }

    /// <inheritdoc />
    public async Task NavigateBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <inheritdoc />
    public async Task NavigateToRootAsync()
    {
        await Shell.Current.GoToAsync("//login");
    }

    /// <inheritdoc />
    public async Task PopToRootAsync()
    {
        await Shell.Current.Navigation.PopToRootAsync();
    }
}