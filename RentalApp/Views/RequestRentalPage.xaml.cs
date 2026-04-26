using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class RequestRentalPage : ContentPage, IQueryAttributable
{
    private readonly RequestRentalViewModel _viewModel;

    public RequestRentalPage(RequestRentalViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Receives Shell route parameters and forwards itemId to the VM.
    /// @details Same pattern as ItemDetailsPage / EditItemPage — keeps the VM
    ///          MAUI-free for testability.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("itemId", out var raw)
            && raw is not null
            && int.TryParse(raw.ToString(), out var id))
        {
            _viewModel.ItemId = id;
        }
    }
}
