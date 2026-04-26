using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class ItemDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ItemDetailsViewModel _viewModel;

    public ItemDetailsPage(ItemDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Receives Shell route parameters and forwards the itemId to the VM.
    /// @details Implementing this here (rather than via [QueryProperty] on the
    ///          VM) keeps the VM MAUI-free so it can compile in the .NET 10
    ///          test project. The VM's OnItemIdChanged hook fires the load.
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
