using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class UserListPage : ContentPage
{
    public UserListPage(UserListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}