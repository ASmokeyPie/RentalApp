using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class EditItemPage : ContentPage, IQueryAttributable
{
    private readonly EditItemViewModel _viewModel;

    public EditItemPage(EditItemViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Receives Shell route parameters and forwards itemId to the VM.
    /// @details Mirrors the pattern used by ItemDetailsPage: route-parameter
    ///          wiring lives on the page so the VM stays MAUI-free for
    ///          testability.
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
