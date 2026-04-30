using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Legacy/utility authentication result DTO that can carry a loaded
/// <see cref="User"/>. Newer flows typically use <see cref="AuthenticationResult"/>
/// plus <see cref="IAuthenticationService.CurrentUser"/>, but this type is kept
/// for compatibility with older call sites.
/// </summary>
public class AuthResult
{
    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Error message suitable for display when <see cref="IsSuccess"/> is false.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>The authenticated user (when <see cref="IsSuccess"/> is true).</summary>
    public User? User { get; set; }

    /// <summary>Create a successful result containing the authenticated user.</summary>
    public static AuthResult Success(User user)
    {
        return new AuthResult
        {
            IsSuccess = true,
            User = user
        };
    }

    /// <summary>Create a failure result with a user-facing error message.</summary>
    public static AuthResult Failure(string errorMessage)
    {
        return new AuthResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}