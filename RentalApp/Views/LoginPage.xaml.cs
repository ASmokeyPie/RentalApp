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
        EmailEntry.Text = "testapi@testapi.com";
        PasswordEntry.Text = "Password@123";
    }
}