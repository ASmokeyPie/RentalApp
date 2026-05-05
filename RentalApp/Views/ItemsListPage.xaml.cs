using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class ItemsListPage : ContentPage
{
    private readonly ItemsListViewModel _viewModel;

    public ItemsListPage(ItemsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Triggers the first-page load and category population when the
    ///        page becomes visible.
    /// @details Categories are loaded once (the VM skips the call when already
    ///          populated). Items are only re-fetched on the first appearance
    ///          so navigating back to this page doesn't reset the scroll
    ///          position unnecessarily.
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load categories into the filter picker (no-op on subsequent calls).
        await _viewModel.LoadCategoriesAsync();

        // Load the first page of items only when the list is empty.
        if (_viewModel.Items.Count == 0)
        {
            await _viewModel.RefreshAsync();
        }
    }
}
