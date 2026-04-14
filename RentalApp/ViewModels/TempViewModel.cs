/// @file TempViewModel.cs
/// @brief Temporary placeholder view model
/// @author RentalApp Development Team
/// @date 2025

using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace RentalApp.ViewModels;

/// @brief Temporary view model for placeholder pages
/// @details Simple view model that displays basic application information
/// @note This is a placeholder implementation for temporary pages
public class TempViewModel
{
    /// @brief Gets the application title from AppInfo
    /// @return The application name as a string
    public string Title => AppInfo.Name;
    
    /// @brief Gets the application version from AppInfo
    /// @return The application version string
    public string Version => AppInfo.VersionString;
    
    /// @brief Gets a placeholder message
    /// @return A message indicating this is a placeholder page
    public string Message => "This is a placeholder page.";

    /// @brief Initializes a new instance of the TempViewModel class
    /// @details Default constructor with no initialization logic
    public TempViewModel()
    {}
}