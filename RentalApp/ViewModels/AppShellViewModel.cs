/// @file AppShellViewModel.cs
/// @brief Application shell view model for managing navigation and authentication state
/// @author RentalApp Development Team
/// @date 2025

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace RentalApp.ViewModels
{
    /// @brief View model for the application shell that manages navigation and authentication
    /// @details Handles menu items, navigation commands, and authentication state changes
    /// @extends BaseViewModel
    public partial class AppShellViewModel : BaseViewModel
    {
        /// @brief Authentication service for managing user authentication
        private readonly IAuthenticationService _authService;
        
        /// @brief Navigation service for managing page navigation
        private readonly INavigationService _navigationService;

        /// @brief Collection of dynamic menu bar items
        /// @details Observable collection that can be modified at runtime based on user permissions
        public ObservableCollection<MenuBarItem> DynamicMenuBarItems { get; } = new();

        /// @brief Default constructor for design-time support
        /// @details Sets the title to "RentalApp"
        public AppShellViewModel()
        {
            Title = "RentalApp";
        }

        /// @brief Initializes a new instance of the AppShellViewModel class
        /// @param authService The authentication service instance
        /// @param navigationService The navigation service instance
        /// @details Sets up authentication state change event handler and initializes the title
        public AppShellViewModel(IAuthenticationService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
            Title = "RentalApp";
        }

        /// @brief Determines if guest actions can be executed
        /// @return True if the current user has the "Guest" role
        private bool CanExecuteGuestAction() => _authService.HasRole("Guest");
        
        /// @brief Determines if user actions can be executed
        /// @return True if the current user has the "OrdinaryUser" role
        private bool CanExecuteUserAction() => _authService.HasRole("OrdinaryUser");
        
        /// @brief Determines if admin actions can be executed
        /// @return True if the current user has the "Admin" role
        private bool CanExecuteAdminAction()
        {
            return _authService.HasRole("Admin");
        }
        
        /// @brief Determines if authenticated actions can be executed
        /// @return True if the user is authenticated
        private bool CanExecuteAuthenticatedAction()
        {
            return _authService.IsAuthenticated;
        }
        
        /// @brief Handles authentication state changes
        /// @param sender The event sender
        /// @param isAuthenticated Whether the user is authenticated
        /// @details Updates command can-execute states and logs authentication information
        private void OnAuthenticationStateChanged(object? sender, bool isAuthenticated)
        {
            LogoutCommand.NotifyCanExecuteChanged();
            NavigateToProfileCommand.NotifyCanExecuteChanged();
            NavigateToSettingsCommand.NotifyCanExecuteChanged();
            Debug.WriteLine($"Authentication state changed: {isAuthenticated}");
            Debug.WriteLine($"Current user is admin: {_authService.HasRole("Admin")}");
        }

        /// @brief Navigates to the current user's profile page
        /// @return A task representing the asynchronous navigation operation
        [RelayCommand]
        private async Task NavigateToProfileAsync()
        {
            await _navigationService.NavigateToAsync("TempPage");
        }

        /// @brief Navigates to the current user's settings page
        /// @return A task representing the asynchronous navigation operation
        [RelayCommand]
        private async Task NavigateToSettingsAsync()
        {
            await _navigationService.NavigateToAsync("TempPage");
        }

        /// @brief Logs out the current user and navigates to login page
        /// @details Relay command that can only be executed by authenticated users
        /// @return A task representing the asynchronous logout operation
        [RelayCommand(CanExecute = nameof(CanExecuteAuthenticatedAction))]
        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            await _navigationService.NavigateToAsync("LoginPage");

            LogoutCommand.NotifyCanExecuteChanged();
            NavigateToProfileCommand.NotifyCanExecuteChanged();
            NavigateToSettingsCommand.NotifyCanExecuteChanged();
        }
    }
}