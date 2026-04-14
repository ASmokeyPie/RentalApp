namespace RentalApp.Services;

public class NavigationService : INavigationService
{
    public async Task NavigateToAsync(string route)
    {
        await Shell.Current.GoToAsync(route);
    }

    public async Task NavigateToAsync(string route, Dictionary<string, object> parameters)
    {
        await Shell.Current.GoToAsync(route, parameters);
    }

    public async Task NavigateBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    public async Task NavigateToRootAsync()
    {
        await Shell.Current.GoToAsync("//login");
    }

    public async Task PopToRootAsync()
    {
        await Shell.Current.Navigation.PopToRootAsync();
    }
}