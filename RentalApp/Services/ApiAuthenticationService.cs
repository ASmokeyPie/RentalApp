using System.Net.Http.Json;
using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Authentication service that talks to the shared hosted API.
///
/// Token handling:
///   * On login we persist a <see cref="StoredToken"/> via <see cref="ITokenStorage"/>.
///   * Outbound requests are authenticated by <see cref="AuthDelegatingHandler"/>,
///     which reads from storage on each call — we never touch
///     <c>HttpClient.DefaultRequestHeaders.Authorization</c> directly.
///   * On a 401 to an authenticated request the handler clears storage and
///     raises <see cref="AuthDelegatingHandler.AuthenticationExpired"/>; we
///     subscribe here to drop the in-memory user and notify the shell.
///   * <see cref="TryRestoreSessionAsync"/> runs on app start: if the stored
///     token is still valid we fetch /users/me and treat that as a restored
///     session.
///
/// The API spec does not define a refresh endpoint, so "handle expiration"
/// means: proactively skip expired tokens, and on 401 drop the session and
/// send the user to Login.
/// </summary>
public class ApiAuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private User? _currentUser;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public bool IsAuthenticated => _currentUser is not null;
    public User? CurrentUser => _currentUser;

    public ApiAuthenticationService(
        HttpClient httpClient,
        ITokenStorage tokenStorage,
        AuthDelegatingHandler authHandler)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        authHandler.AuthenticationExpired += OnAuthenticationExpired;
    }

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/token", new { email, password });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                return new AuthenticationResult(false, error?.Message ?? "Login failed");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse is null)
            {
                return new AuthenticationResult(false, "Login response was empty");
            }

            await _tokenStorage.SaveAsync(new StoredToken(
                tokenResponse.Token,
                tokenResponse.ExpiresAt,
                tokenResponse.UserId));

            // The delegating handler will now attach the token for us.
            var loadedUser = await LoadCurrentUserAsync();
            if (loadedUser is null)
            {
                await _tokenStorage.ClearAsync();
                return new AuthenticationResult(false, "Failed to load user profile after login");
            }

            _currentUser = loadedUser;
            AuthenticationStateChanged?.Invoke(this, true);
            return new AuthenticationResult(true, "Login successful");
        }
        catch (Exception ex)
        {
            return new AuthenticationResult(false, $"Login failed: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register", new
            {
                firstName,
                lastName,
                email,
                password
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                return new AuthenticationResult(false, error?.Message ?? "Registration failed");
            }

            return new AuthenticationResult(true, "Registration successful. Please log in.");
        }
        catch (Exception ex)
        {
            return new AuthenticationResult(false, $"Registration failed: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        await _tokenStorage.ClearAsync();
        _currentUser = null;
        AuthenticationStateChanged?.Invoke(this, false);
    }

    public Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        // Not supported by the shared API.
        return Task.FromResult(false);
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var token = await _tokenStorage.GetValidTokenAsync();
        if (token is null)
        {
            return false;
        }

        var user = await LoadCurrentUserAsync();
        if (user is null)
        {
            // Token was present but server rejected it (handler would have
            // already cleared it on 401) or the call failed another way.
            await _tokenStorage.ClearAsync();
            return false;
        }

        _currentUser = user;
        AuthenticationStateChanged?.Invoke(this, true);
        return true;
    }

    private async Task<User?> LoadCurrentUserAsync()
    {
        var response = await _httpClient.GetAsync("users/me");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        if (profile is null)
        {
            return null;
        }

        return new User
        {
            Id            = profile.Id,
            Email         = profile.Email,
            FirstName     = profile.FirstName,
            LastName      = profile.LastName,
            CreatedAt     = profile.CreatedAt,
            IsActive      = true,
            AverageRating = profile.AverageRating,
        };
    }

    public async Task RefreshCurrentUserAsync()
    {
        if (_currentUser is null) return;

        var user = await LoadCurrentUserAsync();
        if (user is not null)
        {
            _currentUser = user;
        }
    }

    private void OnAuthenticationExpired(object? sender, EventArgs e)
    {
        // The handler already cleared the token in storage. We just need
        // to drop the in-memory user and fan out the event so the shell
        // can redirect to Login.
        if (_currentUser is null)
        {
            return;
        }

        _currentUser = null;
        AuthenticationStateChanged?.Invoke(this, false);
    }

    // --- API response DTOs ---

    private record TokenResponse(string Token, DateTime ExpiresAt, int UserId);

    private record UserProfileResponse(
        int Id, string Email, string FirstName, string LastName,
        double? AverageRating, DateTime CreatedAt);

    private record ApiErrorResponse(string Error, string Message);
}
