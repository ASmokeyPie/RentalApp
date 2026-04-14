using RentalApp.ViewModels;

namespace RentalApp;

public partial class AppShell : Shell
{
	public AppShell(AppShellViewModel viewModel)
	{	
		BindingContext = viewModel;
		InitializeComponent();
	}
}
