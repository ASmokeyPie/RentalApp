/// @file BaseViewModel.cs
/// @brief Base view model class providing common functionality for all view models
/// @author RentalApp Development Team
/// @date 2025

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RentalApp.ViewModels;

/// @brief Base view model class that provides common properties and functionality
/// @details Extends ObservableObject to provide property change notifications and includes
/// common properties like IsBusy, Title, and error handling
/// @extends ObservableObject
public partial class BaseViewModel : ObservableObject
{
    /// @brief Indicates whether the view model is currently performing a busy operation
    /// @details Observable property that can be bound to UI elements to show loading states
    [ObservableProperty]
    private bool isBusy;

    /// @brief The title of the current page or view
    /// @details Observable property that can be bound to page titles or headers
    [ObservableProperty]
    private string title = string.Empty;

    /// @brief The current error message, if any
    /// @details Observable property that stores error messages for display to the user
    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// @brief Indicates whether there is currently an error
    /// @details Observable property that indicates if an error state exists
    [ObservableProperty]
    private bool hasError;

    /// @brief Sets an error message and updates the error state
    /// @param message The error message to set
    /// @details Updates both ErrorMessage and HasError properties
    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = !string.IsNullOrEmpty(message);
    }

    /// @brief Clears the current error state
    /// @details Resets both ErrorMessage and HasError properties
    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    /// @brief Command to clear the current error state
    /// @details Relay command that calls ClearError method
    [RelayCommand]
    private void ClearErrorCommand()
    {
        ClearError();
    }
}