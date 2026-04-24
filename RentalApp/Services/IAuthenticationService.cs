using RentalApp.Database.Models;

namespace RentalApp.Services;

public interface IAuthenticationService
{
    event EventHandler<bool>? AuthenticationStateChanged;

    bool IsAuthenticated { get; }
    User? CurrentUser { get; }

    Task<AuthenticationResult> LoginAsync(string email, string password);
    Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password);
    Task LogoutAsync();

    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);

    /// <summary>
    /// Attempts to restore an authenticated session from persisted storage
    /// without prompting the user. Called on app start. Returns true if a
    /// valid session was restored (and AuthenticationStateChanged has been
    /// raised); false if the user must log in again.
    /// </summary>
    Task<bool> TryRestoreSessionAsync();
}