using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reload on every appearance so the profile stays fresh if the user
        // navigates away and comes back (e.g. after writing a new review).
        _ = _viewModel.RefreshAsync();
    }
}
