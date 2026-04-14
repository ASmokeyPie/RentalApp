using RentalApp.Database.Models;

namespace RentalApp.Services;

public class AuthResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public User? User { get; set; }
    public List<string> Roles { get; set; } = new();

    public static AuthResult Success(User user, List<string> roles)
    {
        return new AuthResult
        {
            IsSuccess = true,
            User = user,
            Roles = roles
        };
    }

    public static AuthResult Failure(string errorMessage)
    {
        return new AuthResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}