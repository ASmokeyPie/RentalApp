/// @file ProfileViewModel.cs
/// @brief User profile management view model
/// @author RentalApp Development Team
/// @date 2025

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the user profile page
/// @details Manages user profile display and password change functionality
/// @extends BaseViewModel
public partial class ProfileViewModel : BaseViewModel
{
    /// @brief Authentication service for managing user authentication
    private readonly IAuthenticationService _authService;
    
    /// @brief Navigation service for managing page navigation
    private readonly INavigationService _navigationService;

    /// @brief The current user's profile information
    /// @details Observable property containing the current user's data
    [ObservableProperty]
    private User? currentUser;

    /// @brief The user's current password for verification
    /// @details Observable property bound to the current password input field
    [ObservableProperty]
    private string currentPassword = string.Empty;

    /// @brief The user's new password
    /// @details Observable property bound to the new password input field
    [ObservableProperty]
    private string newPassword = string.Empty;

    /// @brief Confirmation of the user's new password
    /// @details Observable property bound to the confirm new password input field
    [ObservableProperty]
    private string confirmNewPassword = string.Empty;

    /// @brief Indicates whether the password change mode is active
    /// @details Observable property that controls the visibility of password change fields
    [ObservableProperty]
    private bool isChangingPassword;

    /// @brief Initializes a new instance of the ProfileViewModel class
    /// @param authService The authentication service instance
    /// @param navigationService The navigation service instance
    /// @details Sets up the required services, initializes the title, and loads user data
    public ProfileViewModel(IAuthenticationService authService, INavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        Title = "Profile";

        LoadUserData();
    }

    /// @brief Loads the current user's profile data
    /// @details Retrieves the current user's information from the authentication service
    private void LoadUserData()
    {
        CurrentUser = _authService.CurrentUser;
    }

    /// @brief Changes the user's password
    /// @details Relay command that validates and performs the password change operation
    /// @return A task representing the asynchronous password change operation
    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (IsBusy)
            return;

        if (!ValidatePasswordChange())
            return;

        try
        {
            IsBusy = true;
            ClearError();

            var success = await _authService.ChangePasswordAsync(CurrentPassword, NewPassword);

            if (success)
            {
                await Application.Current.MainPage.DisplayAlert("Success", "Password changed successfully!", "OK");
                ClearPasswordFields();
                IsChangingPassword = false;
            }
            else
            {
                SetError("Failed to change password. Please check your current password.");
            }
        }
        catch (Exception ex)
        {
            SetError($"Password change failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Toggles the password change mode
    /// @details Relay command that shows/hides password change fields and clears data when hiding
    [RelayCommand]
    private void TogglePasswordChangeMode()
    {
        IsChangingPassword = !IsChangingPassword;
        if (!IsChangingPassword)
        {
            ClearPasswordFields();
            ClearError();
        }
    }

    /// @brief Navigates back to the previous page
    /// @details Relay command that performs backward navigation
    /// @return A task representing the asynchronous navigation operation
    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        await _navigationService.NavigateBackAsync();
    }

    /// @brief Validates the password change form data
    /// @return True if validation passes, false otherwise
    /// @details Checks all password change requirements and sets appropriate error messages
    private bool ValidatePasswordChange()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            SetError("Current password is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            SetError("New password is required");
            return false;
        }

        if (NewPassword.Length < 6)
        {
            SetError("New password must be at least 6 characters long");
            return false;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            SetError("New passwords do not match");
            return false;
        }

        if (CurrentPassword == NewPassword)
        {
            SetError("New password must be different from current password");
            return false;
        }

        return true;
    }

    /// @brief Clears all password input fields
    /// @details Resets all password-related properties to empty strings
    private void ClearPasswordFields()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
    }
}