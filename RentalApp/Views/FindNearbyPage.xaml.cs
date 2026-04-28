using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class FindNearbyPage : ContentPage
{
    private readonly FindNearbyViewModel _viewModel;

    public FindNearbyPage(FindNearbyViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Loads categories on first appearance.
    /// @details The first GPS fetch happens when the user taps "Find nearby
    ///          items" — we don't auto-prompt for location permission on
    ///          page-open because that's a surprising experience.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Categories.Count <= 1)  // only the synthetic "All" entry
        {
            await _viewModel.LoadCategoriesAsync();
        }
    }
}
