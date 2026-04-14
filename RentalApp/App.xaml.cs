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
		Routing.RegisterRoute(nameof(Views.UserListPage), typeof(Views.UserListPage));
		Routing.RegisterRoute(nameof(Views.UserDetailPage), typeof(Views.UserDetailPage));
		Routing.RegisterRoute(nameof(Views.TempPage), typeof(Views.TempPage));
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
		return window;
	}
}
