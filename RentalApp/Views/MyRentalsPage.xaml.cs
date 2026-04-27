using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class MyRentalsPage : ContentPage
{
    private readonly MyRentalsViewModel _viewModel;

    public MyRentalsPage(MyRentalsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Loads both lists on first appearance.
    /// @details Skips on subsequent appearances if either collection already
    ///          has items — pull-to-refresh covers re-load explicitly.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Incoming.Count == 0 && _viewModel.Outgoing.Count == 0)
        {
            await _viewModel.RefreshAsync();
        }
    }
}
