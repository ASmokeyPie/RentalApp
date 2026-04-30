using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Authentication/session contract consumed by ViewModels.
///
/// Two implementations exist:
/// <list type="bullet">
///   <item><description><see cref="ApiAuthenticationService"/> — talks to the shared hosted API and persists a JWT.</description></item>
///   <item><description><see cref="AuthenticationService"/> — local/offline mode backed by the on-device database.</description></item>
/// </list>
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Raised whenever the authenticated state changes.
    /// The boolean argument is <c>true</c> when signed in; <c>false</c> when signed out.
    /// </summary>
    event EventHandler<bool>? AuthenticationStateChanged;

    /// <summary>True when there is an active in-memory session.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The current authenticated user, or <c>null</c> if signed out.</summary>
    User? CurrentUser { get; }

    /// <summary>Attempts to sign in with the given credentials.</summary>
    Task<AuthenticationResult> LoginAsync(string email, string password);

    /// <summary>Creates a new user account.</summary>
    Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password);

    /// <summary>Clears any persisted credentials (if applicable) and ends the session.</summary>
    Task LogoutAsync();

    /// <summary>
    /// Attempts to change the current user's password.
    /// Implementations may return <c>false</c> if the backing auth system doesn't support this.
    /// </summary>
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);

    /// <summary>
    /// Attempts to restore an authenticated session from persisted storage
    /// without prompting the user. Called on app start. Returns true if a
    /// valid session was restored (and AuthenticationStateChanged has been
    /// raised); false if the user must log in again.
    /// </summary>
    Task<bool> TryRestoreSessionAsync();

    /// <summary>
    /// Re-fetches the current user's profile from GET /users/me and updates
    /// <see cref="CurrentUser"/> in place. Used by pages (e.g. ProfilePage)
    /// that need a fresh snapshot — including fields like
    /// <see cref="Database.Models.User.AverageRating"/> that are not loaded
    /// at login time in older code. No-op if not authenticated.
    /// </summary>
    Task RefreshCurrentUserAsync();
}