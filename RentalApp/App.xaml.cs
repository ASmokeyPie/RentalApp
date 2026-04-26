using System.Diagnostics;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp;

public partial class App : Application
{
	private readonly IServiceProvider _serviceProvider;
	public App(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
		InitializeComponent();

		Routing.RegisterRoute(nameof(Views.MainPage), typeof(Views.MainPage));
		Routing.RegisterRoute(nameof(Views.LoginPage), typeof(Views.LoginPage));
		Routing.RegisterRoute(nameof(Views.RegisterPage), typeof(Views.RegisterPage));
		Routing.RegisterRoute(nameof(Views.TempPage), typeof(Views.TempPage));
		Routing.RegisterRoute(nameof(Views.ItemsListPage), typeof(Views.ItemsListPage));
		Routing.RegisterRoute(nameof(Views.ItemDetailsPage), typeof(Views.ItemDetailsPage));
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// var window = base.CreateWindow(activationState);
		// window.Page = new AppShell();

		var shell = _serviceProvider.GetService<AppShell>();
		if (shell == null)
		{
			// Handle the error if AppShell could not be resolved
			throw new InvalidOperationException("AppShell could not be resolved from the service provider.");
		}
		var window = new Window(shell);

		// Fire-and-forget: attempt to restore a persisted session on startup.
		// If successful, IAuthenticationService raises AuthenticationStateChanged
		// and subscribers (shell view model, pages) react. If not, the shell
		// stays on its default LoginPage content.
		_ = Task.Run(async () =>
		{
			try
			{
				var auth = _serviceProvider.GetRequiredService<IAuthenticationService>();
				await auth.TryRestoreSessionAsync();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Session restore failed: {ex.Message}");
			}
		});

		return window;
	}
}
