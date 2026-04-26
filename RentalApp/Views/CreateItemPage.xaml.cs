using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class CreateItemPage : ContentPage
{
    private readonly CreateItemViewModel _viewModel;

    public CreateItemPage(CreateItemViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Loads categories on first appearance.
    /// @details Skips the load when categories are already cached on the VM
    ///          so navigating back to the page doesn't refetch unnecessarily.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Categories.Count == 0)
        {
            await _viewModel.LoadCategoriesAsync();
        }
    }
}
