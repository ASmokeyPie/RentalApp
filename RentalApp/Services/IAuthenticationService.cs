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
}