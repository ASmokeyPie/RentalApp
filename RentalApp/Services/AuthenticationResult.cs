namespace RentalApp.Services;

/// <summary>
/// Outcome of an authentication operation (login, register).
/// <see cref="Message"/> is safe to display to the user.
/// </summary>
public class AuthenticationResult
{
    public bool IsSuccess { get; }
    public string Message { get; }

    public AuthenticationResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }
}
