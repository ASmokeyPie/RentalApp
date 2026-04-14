/// @file MainViewModel.cs
/// @brief Main dashboard view model for authenticated users
/// @author RentalApp Development Team
/// @date 2025

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the main dashboard page
/// @details Manages the main dashboard display, user information, and navigation to other sections
/// @extends BaseViewModel
public partial class MainViewModel : BaseViewModel
{
    /// @brief Authentication service for managing user authentication
    private readonly IAuthenticationService _authService;
    
    /// @brief Navigation service for managing page navigation
    private readonly INavigationService _navigationService;

    /// @brief The currently authenticated user
    /// @details Observable property containing the current user's information
    [ObservableProperty]
    private User? currentUser;

    /// @brief Welcome message displayed to the user
    /// @details Observable property showing a personalized welcome message
    [ObservableProperty]
    private string welcomeMessage = string.Empty;

    /// @brief Indicates whether the current user has admin privileges
    /// @details Observable property used to control visibility of admin features
    [ObservableProperty]
    private bool isAdmin;

    /// @brief Default constructor for design-time support
    /// @details Sets the title to "Dashboard"
    public MainViewModel()
        {
            // Default constructor for design time support
            Title = "Dashboard";
        }
    
    /// @brief Initializes a new instance of the MainViewModel class
    /// @param authService The authentication service instance
    /// @param navigationService The navigation service instance
    /// @details Sets up the required services, initializes the title, and loads user data
    public MainViewModel(IAuthenticationService authService, INavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        Title = "Dashboard";

        LoadUserData();
    }

    /// @brief Loads the current user's data and sets up the dashboard
    /// @details Retrieves current user information and determines admin status
    private void LoadUserData()
    {
        CurrentUser = _authService.CurrentUser;
        IsAdmin = _authService.HasRole("Admin");
        
        if (CurrentUser != null)
        {
            WelcomeMessage = $"Welcome, {CurrentUser.FullName}!";
        }
    }

    /// @brief Logs out the current user
    /// @details Relay command that confirms logout and performs the logout operation
    /// @return A task representing the asynchronous logout operation
    [RelayCommand]
    private async Task LogoutAsync()
    {
        var result = await Application.Current.MainPage.DisplayAlert(
            "Logout", 
            "Are you sure you want to logout?", 
            "Yes", 
            "No");

        if (result)
        {
            await _authService.LogoutAsync();
            await _navigationService.NavigateToAsync("LoginPage");
        }
    }

    /// @brief Navigates to the user profile page
    /// @details Relay command that navigates to the profile management page
    /// @return A task representing the asynchronous navigation operation
    [RelayCommand]
    private async Task NavigateToProfileAsync()
    {
        await _navigationService.NavigateToAsync("TempPage");
    }

    /// @brief Navigates to the settings page
    /// @details Relay command that navigates to the application settings page
    /// @return A task representing the asynchronous navigation operation
    [RelayCommand]
    private async Task NavigateToSettingsAsync()
    {
        await _navigationService.NavigateToAsync("TempPage");
    }


    /// @brief Navigates to the user list page
    /// @details Relay command that navigates to the user management page, admin only
    /// @return A task representing the asynchronous navigation operation
    [RelayCommand]
    private async Task NavigateToUserListAsync()
    {
        if (!IsAdmin)
        {
            await Application.Current.MainPage.DisplayAlert("Access Denied", "You don't have permission to access admin features.", "OK");
            return;
        }
        
        await _navigationService.NavigateToAsync("UserListPage");
    }

    /// @brief Refreshes the dashboard data
    /// @details Relay command that reloads user data and simulates a refresh operation
    /// @return A task representing the asynchronous refresh operation
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        try
        {
            IsBusy = true;
            LoadUserData();
            
            // Simulate refresh delay
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            SetError($"Failed to refresh data: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}