using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;
using BCrypt.Net;

namespace RentalApp.Services;

/// <summary>
/// Local/offline <see cref="IAuthenticationService"/> implementation backed by the
/// on-device database (<see cref="AppDbContext"/>). Uses BCrypt to verify and
/// store password hashes.
///
/// This service is for the "local DB" mode; the shared hosted API mode uses
/// <see cref="ApiAuthenticationService"/>.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private User? _currentUser;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public AuthenticationService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public bool IsAuthenticated => _currentUser != null;

    public User? CurrentUser => _currentUser;

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        try
        {
            using var context = _factory.CreateDbContext();
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            if (user == null)
            {
                return new AuthenticationResult(false, "Invalid email or password");
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return new AuthenticationResult(false, "Invalid email or password");
            }

            _currentUser = user;

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
            using var context = _factory.CreateDbContext();

            // Check if user already exists
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                return new AuthenticationResult(false, "User with this email already exists");
            }

            // Create password hash
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, salt);

            // Create new user
            var user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PasswordHash = hashedPassword,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();


            return new AuthenticationResult(true, "Registration successful");
        }
        catch (Exception ex)
        {
            return new AuthenticationResult(false, $"Registration failed: {ex.Message}");
        }
    }

    public Task LogoutAsync()
    {
        _currentUser = null;
        AuthenticationStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Offline/local auth has no persisted session — the user signs in fresh
    /// every app launch. This is the local-database code path and is not used
    /// while `useSharedApi = true` in MauiProgram.
    /// </summary>
    public Task<bool> TryRestoreSessionAsync() => Task.FromResult(false);

    /// <summary>
    /// The local-DB path has no remote /users/me endpoint, so there is nothing
    /// to refresh. AverageRating is not computed by the local service.
    /// </summary>
    public Task RefreshCurrentUserAsync() => Task.CompletedTask;

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (_currentUser == null)
            return false;

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, _currentUser.PasswordHash))
                return false;

            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword, salt);

            // Open a fresh context — the user entity was loaded in a previous
            // (now-disposed) context, so we fetch by ID and update there.
            using var context = _factory.CreateDbContext();
            var user = await context.Users.FindAsync(_currentUser.Id);
            if (user is null) return false;

            user.PasswordHash = hashedPassword;
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            // Keep the in-memory copy consistent.
            _currentUser.PasswordHash = hashedPassword;
            _currentUser.UpdatedAt = user.UpdatedAt;

            return true;
        }
        catch
        {
            return false;
        }
    }
}