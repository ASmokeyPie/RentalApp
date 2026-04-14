/// @file LoginViewModel.cs
/// @brief Login page view model for user authentication
/// @author RentalApp Development Team
/// @date 2025

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the login page that handles user authentication
/// @details Manages login form data, validation, and authentication process
/// @extends BaseViewModel
public partial class LoginViewModel : BaseViewModel
{
    /// @brief Authentication service for managing user login
    private readonly IAuthenticationService _authService;
    
    /// @brief Navigation service for managing page navigation
    private readonly INavigationService _navigationService;

    /// @brief The user's email address
    /// @details Observable property bound to the email input field
    [ObservableProperty]
    private string email = string.Empty;

    /// @brief The user's password
    /// @details Observable property bound to the password input field
    [ObservableProperty]
    private string password = string.Empty;

    /// @brief Whether to remember the user's login credentials
    /// @details Observable property bound to the remember me checkbox
    [ObservableProperty]
    private bool rememberMe;

    /// @brief Indicates whether a login operation is in progress
    /// @details Observable property that notifies the LoginCommand when changed
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    /// @brief Default constructor for design-time support
    /// @details Sets the title to "Login"
    public LoginViewModel()
    {
        // Default constructor for design time support
        Title = "Login";
    }

    /// @brief Initializes a new instance of the LoginViewModel class
    /// @param authService The authentication service instance
    /// @param navigationService The navigation service instance
    /// @details Sets up the required services and initializes the title
    public LoginViewModel(IAuthenticationService authService, INavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        Title = "Login";
    }

    /// @brief Performs user login authentication
    /// @details Relay command that validates input and attempts to authenticate the user
    /// @return A task representing the asynchronous login operation
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Please enter both email and password");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var result = await _authService.LoginAsync(Email, Password);

            if (result.IsSuccess)
            {
                await _navigationService.NavigateToAsync("MainPage");
            }
            else
            {
                SetError(result.Message);
            }
        }
        catch (Exception ex)
        {
            SetError($"Login failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Navigates to the registration page
    /// @details Relay command that navigates to the user registration page
    /// @return A task representing the asynchronous navigation operation
    [RelayCommand]
    private async Task NavigateToRegisterAsync()
    {
        await _navigationService.NavigateToAsync("RegisterPage");
    }

    /// @brief Handles forgot password functionality
    /// @details Relay command that displays a placeholder message for forgot password
    /// @return A task representing the asynchronous operation
    /// @todo Implement actual forgot password functionality
    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        // TODO: Implement forgot password functionality
        await Application.Current.MainPage.DisplayAlert("Info", "Forgot password functionality not implemented yet", "OK");
    }
}