using System.Net.Http.Json;
using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Authentication service that talks to the shared hosted API.
///
/// Token handling:
///   - On login, persist a <see cref="StoredToken"/> via <see cref="ITokenStorage"/>.
///   - Outbound requests are authenticated by <see cref="AuthDelegatingHandler"/>,
///     which reads from storage on each call — never touching
///     <c>HttpClient.DefaultRequestHeaders.Authorization</c> directly.
///   - On a 401 to an authenticated request the handler clears storage and
///     raises <see cref="AuthDelegatingHandler.AuthenticationExpired"/>;
///     subscribe here to drop the in-memory user and notify the shell.
///   - <see cref="TryRestoreSessionAsync"/> runs on app start: if the stored
///     token is still valid, fetch /users/me and treat that as a restored
///     session.
///
/// Expired tokens are proactively skipped, and 401 drops the session and
/// sends the user to Login.
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
        // HttpClient is configured in MauiProgram with the API base address.
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;

        // Subscribe to the pipeline-level "token rejected" signal (401 on an
        // authenticated request) so the in-memory session can be dropped.
        authHandler.AuthenticationExpired += OnAuthenticationExpired;
    }

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        try
        {
            // Request a JWT from the API. No token attahced to this call.
            var response = await _httpClient.PostAsJsonAsync("auth/token", new { email, password });

            if (!response.IsSuccessStatusCode)
            {
                // API returns a structured error body; surface its message to the UI.
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                return new AuthenticationResult(false, error?.Message ?? "Login failed");
            }

            // Successful response should include the JWT + expiry + user id.
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse is null)
            {
                return new AuthenticationResult(false, "Login response was empty");
            }

            // Persist the token for future launches and for the delegating handler.
            await _tokenStorage.SaveAsync(new StoredToken(
                tokenResponse.Token,
                tokenResponse.ExpiresAt,
                tokenResponse.UserId));

            // The delegating handler will now attach the token.
            // Immediately hydrate the current user so the UI can bind to profile fields.
            var loadedUser = await LoadCurrentUserAsync();
            if (loadedUser is null)
            {
                // If the profile can't be loaded, treat this as a failed login and
                // clear storage to avoid a half-signed-in state.
                await _tokenStorage.ClearAsync();
                return new AuthenticationResult(false, "Failed to load user profile after login");
            }

            // Store in-memory session state and notify listeners (Shell, ViewModels).
            _currentUser = loadedUser;
            AuthenticationStateChanged?.Invoke(this, true);
            return new AuthenticationResult(true, "Login successful");
        }
        catch (Exception ex)
        {
            // Network failures, deserialisation issues, etc.
            return new AuthenticationResult(false, $"Login failed: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password)
    {
        try
        {
            // Create the user account. The API does not auto-login; caller should
            // navigate to Login afterwards.
            var response = await _httpClient.PostAsJsonAsync("auth/register", new
            {
                firstName,
                lastName,
                email,
                password
            });

            if (!response.IsSuccessStatusCode)
            {
                // Surface API-provided validation or conflict messages to the UI.
                var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                return new AuthenticationResult(false, error?.Message ?? "Registration failed");
            }

            return new AuthenticationResult(true, "Registration successful. Please log in.");
        }
        catch (Exception ex)
        {
            // Network failures, serialization issues, etc.
            return new AuthenticationResult(false, $"Registration failed: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        // Clear persisted credentials and in-memory session state.
        await _tokenStorage.ClearAsync();
        _currentUser = null;

        // Notify listeners so the shell can redirect to Login.
        AuthenticationStateChanged?.Invoke(this, false);
    }

    public Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        // Not supported by the shared API.
        return Task.FromResult(false);
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        // Load token only if it's present and not expired (proactive check).
        var token = await _tokenStorage.GetValidTokenAsync();
        if (token is null)
        {
            return false;
        }

        // Token looks usable — verify it with the server by loading /users/me.
        var user = await LoadCurrentUserAsync();
        if (user is null)
        {
            // Token was present but server rejected it (handler would have
            // already cleared it on 401) or the call failed another way.
            await _tokenStorage.ClearAsync();
            return false;
        }

        // Session successfully restored; update in-memory user and notify.
        _currentUser = user;
        AuthenticationStateChanged?.Invoke(this, true);
        return true;
    }

    private async Task<User?> LoadCurrentUserAsync()
    {
        // Server-authoritative profile for the current token.
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

        // Map API DTO -> shared User model used across app layers.
        return new User
        {
            Id = profile.Id,
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            CreatedAt = profile.CreatedAt,
            IsActive = true,
            AverageRating = profile.AverageRating,
        };
    }

    public async Task RefreshCurrentUserAsync()
    {
        // No-op if signed out.
        if (_currentUser is null) return;

        // Fetch a fresh snapshot; useful after profile-affecting operations.
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

        // Drop the session and notify subscribers.
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
