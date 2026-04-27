using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class RentalDetailsPage : ContentPage, IQueryAttributable
{
    private readonly RentalDetailsViewModel _viewModel;

    public RentalDetailsPage(RentalDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// @brief Receives Shell route parameters and forwards rentalId to the VM.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("rentalId", out var raw)
            && raw is not null
            && int.TryParse(raw.ToString(), out var id))
        {
            _viewModel.RentalId = id;
        }
    }
}
