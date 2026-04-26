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

    /// @brief Triggers the first-page load when the page becomes visible.
    /// @details Avoids re-loading on every appearance — only kicks off when
    ///          the list is empty (first appearance, or after navigation back
    ///          from somewhere that didn't leave items in memory).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Items.Count == 0)
        {
            await _viewModel.RefreshAsync();
        }
    }
}
