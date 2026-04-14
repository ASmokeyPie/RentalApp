using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        EmailEntry.Focus();
        EmailEntry.Text = "admin@company.com";
        PasswordEntry.Text = "Admin123!";
    }
}