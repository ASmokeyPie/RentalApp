using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;
using BCrypt.Net;

namespace RentalApp.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly AppDbContext _context;
    private User? _currentUser;
    private List<string> _currentUserRoles = new();

    public event EventHandler<bool>? AuthenticationStateChanged;

    public AuthenticationService(AppDbContext context)
    {
        _context = context;
    }

    public bool IsAuthenticated => _currentUser != null;

    public User? CurrentUser => _currentUser;

    public List<string> CurrentUserRoles => _currentUserRoles;

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
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
            _currentUserRoles = user.UserRoles
                .Where(ur => ur.IsActive)
                .Select(ur => ur.Role.Name)
                .ToList();

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
            // Check if user already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
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
                PasswordSalt = salt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Assign default "User" role
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.IsDefault == true);
            if (userRole != null)
            {
                var userRoleAssignment = new UserRole(user.Id, userRole.Id);
                _context.UserRoles.Add(userRoleAssignment);
                await _context.SaveChangesAsync();
            }

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
        _currentUserRoles.Clear();
        AuthenticationStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public bool HasRole(string roleName)
    {
        return _currentUserRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAnyRole(params string[] roleNames)
    {
        return roleNames.Any(role => HasRole(role));
    }

    public bool HasAllRoles(params string[] roleNames)
    {
        return roleNames.All(role => HasRole(role));
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (_currentUser == null)
            return false;

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, _currentUser.PasswordHash))
            {
                return false;
            }

            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword, salt);

            _currentUser.PasswordHash = hashedPassword;
            _currentUser.PasswordSalt = salt;
            _currentUser.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(_currentUser);
            await _context.SaveChangesAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }
}

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