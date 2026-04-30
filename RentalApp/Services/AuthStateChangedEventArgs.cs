using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Event payload describing a change in authentication state.
///
/// Note: many components use <see cref="IAuthenticationService.AuthenticationStateChanged"/>
/// which passes a boolean directly; this type exists for any flows that need
/// to include the current <see cref="User"/> as part of the event.
/// </summary>
public class AuthStateChangedEventArgs : EventArgs
{
    /// <summary>True when the app has an authenticated user session.</summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>The authenticated user, or <c>null</c> when signed out.</summary>
    public User? User { get; set; }
}