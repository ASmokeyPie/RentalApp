using RentalApp.Database.Models;

namespace RentalApp.Services;

public interface IAuthenticationService
{
    event EventHandler<bool>? AuthenticationStateChanged;
    
    bool IsAuthenticated { get; }
    User? CurrentUser { get; }
    List<string> CurrentUserRoles { get; }
    
    Task<AuthenticationResult> LoginAsync(string email, string password);
    Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password);
    Task LogoutAsync();
    
    bool HasRole(string roleName);
    bool HasAnyRole(params string[] roleNames);
    bool HasAllRoles(params string[] roleNames);
    
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);
}