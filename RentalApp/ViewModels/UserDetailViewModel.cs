using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;
using RentalApp.Services;
using CommunityToolkit.Mvvm.Input;

namespace RentalApp.ViewModels;

/// <summary>
/// ViewModel for managing user details including creation, editing, and role management.
/// Handles both new user creation and existing user modification scenarios.
/// </summary>
/// <remarks>
/// This ViewModel provides comprehensive user management functionality including:
/// - User creation with password validation
/// - User profile editing
/// - Role assignment and removal
/// - User deletion (soft delete)
/// - Input validation and error handling
/// </remarks>
[QueryProperty(nameof(UserId), "userId")]
public partial class UserDetailViewModel : INotifyPropertyChanged
{
    #region Private Fields
    
    /// <summary>Database context for data operations</summary>
    private readonly AppDbContext _context;
    
    /// <summary>Navigation service for page transitions</summary>
    private readonly INavigationService _navigationService;
    
    /// <summary>Authentication service for user role verification</summary>
    private readonly IAuthenticationService _authService;

    /// <summary>The ID of the user being edited (0 for new users)</summary>
    private int _userId;
    
    /// <summary>The current user entity being edited</summary>
    private User? _currentUser;
    
    /// <summary>User's first name</summary>
    private string _firstName = string.Empty;
    
    /// <summary>User's last name</summary>
    private string _lastName = string.Empty;
    
    /// <summary>User's email address</summary>
    private string _email = string.Empty;
    
    /// <summary>User's password (for new users only)</summary>
    private string _password = string.Empty;
    
    /// <summary>Password confirmation field</summary>
    private string _confirmPassword = string.Empty;
    
    /// <summary>Whether the user account is active</summary>
    private bool _isActive = true;
    
    /// <summary>Loading state indicator</summary>
    private bool _isLoading = false;
    
    /// <summary>Flag indicating if this is a new user creation</summary>
    private bool _isNewUser = false;
    
    /// <summary>Error message to display to user</summary>
    private string _errorMessage = string.Empty;
    
    /// <summary>Success message to display to user</summary>
    private string _successMessage = string.Empty;
    
    /// <summary>Collection of all available roles for assignment</summary>
    private ObservableCollection<RoleItem> _availableRoles = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the UserDetailViewModel class.
    /// </summary>
    /// <param name="context">The database context for data operations</param>
    /// <param name="navigationService">The navigation service for page transitions</param>
    /// <param name="authService">The authentication service for role verification</param>
    public UserDetailViewModel(AppDbContext context, INavigationService navigationService, IAuthenticationService authService)
    {
        _context = context;
        _navigationService = navigationService;
        _authService = authService;

        SaveUserCommand = new Command(async () => await SaveUserAsync(), CanSaveUser);
        DeleteUserCommand = new Command(async () => await DeleteUserAsync(), CanDeleteUser);
        AddRoleCommand = new Command<RoleItem>(async (role) => await AddRoleAsync(role));
        RemoveRoleCommand = new Command<RoleItem>(async (role) => await RemoveRoleAsync(role));
        BackCommand = new Command(async () => await NavigateBackAsync());

        PropertyChanged += (s, e) => 
        {
            ((Command)SaveUserCommand).ChangeCanExecute();
            ((Command)DeleteUserCommand).ChangeCanExecute();
        };
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the user ID. Setting this property triggers user loading.
    /// </summary>
    /// <value>The user ID (0 for new users)</value>
    public int UserId
    {
        get => _userId;
        set
        {
            _userId = value;
            OnPropertyChanged();
            _ = Task.Run(LoadUserAsync);
        }
    }

    /// <summary>
    /// Gets or sets the user's first name.
    /// </summary>
    /// <value>The first name of the user</value>
    public string FirstName
    {
        get => _firstName;
        set
        {
            _firstName = value;
            OnPropertyChanged();
            ClearMessages();
        }
    }

    /// <summary>
    /// Gets or sets the user's last name.
    /// </summary>
    /// <value>The last name of the user</value>
    public string LastName
    {
        get => _lastName;
        set
        {
            _lastName = value;
            OnPropertyChanged();
            ClearMessages();
        }
    }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    /// <value>The email address of the user</value>
    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
            ClearMessages();
        }
    }

    /// <summary>
    /// Gets or sets the user's password (visible only for new users).
    /// </summary>
    /// <value>The password for new user accounts</value>
    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
            ClearMessages();
        }
    }

    /// <summary>
    /// Gets or sets the password confirmation field.
    /// </summary>
    /// <value>The password confirmation string</value>
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            _confirmPassword = value;
            OnPropertyChanged();
            ClearMessages();
        }
    }

    /// <summary>
    /// Gets or sets whether the user account is active.
    /// </summary>
    /// <value>True if the user account is active, false otherwise</value>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the loading state of the view model.
    /// </summary>
    /// <value>True if an operation is in progress, false otherwise</value>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether this is a new user creation scenario.
    /// </summary>
    /// <value>True if creating a new user, false if editing existing user</value>
    public bool IsNewUser
    {
        get => _isNewUser;
        set
        {
            _isNewUser = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the error message to display to the user.
    /// </summary>
    /// <value>The error message string</value>
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the success message to display to the user.
    /// </summary>
    /// <value>The success message string</value>
    public string SuccessMessage
    {
        get => _successMessage;
        set
        {
            _successMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the collection of available roles for assignment.
    /// </summary>
    /// <value>Observable collection of role items</value>
    public ObservableCollection<RoleItem> AvailableRoles
    {
        get => _availableRoles;
        set
        {
            _availableRoles = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets the page title based on whether this is a new user or edit scenario.
    /// </summary>
    /// <value>"Create New User" for new users, "Edit User" for existing users</value>
    public string PageTitle => IsNewUser ? "Create New User" : "Edit User";
    
    /// <summary>
    /// Gets whether password fields should be shown (only for new users).
    /// </summary>
    /// <value>True if password fields should be visible, false otherwise</value>
    public bool ShowPasswordFields => IsNewUser;
    
    /// <summary>
    /// Gets whether the current user can be deleted (not new user and not the currently logged-in user).
    /// </summary>
    /// <value>True if the user can be deleted, false otherwise</value>
    public bool CanDeleteCurrentUser => !IsNewUser && _currentUser?.Id != _authService.CurrentUser?.Id;

    #endregion

    #region Commands

    /// <summary>Command to save the user (create or update)</summary>
    public ICommand SaveUserCommand { get; }
    
    /// <summary>Command to delete the user</summary>
    public ICommand DeleteUserCommand { get; }
    
    /// <summary>Command to add a role to the user</summary>
    public ICommand AddRoleCommand { get; }
    
    /// <summary>Command to remove a role from the user</summary>
    public ICommand RemoveRoleCommand { get; }
    
    /// <summary>Command to navigate back to the user list</summary>
    public ICommand BackCommand { get; }

    #endregion

    #region Private Methods

    /// @brief Navigates to the dashboard page
    /// @return A task representing the asynchronous navigation operation
    [RelayCommand]
    private async Task NavigateToDashboardAsync()
    {
        await _navigationService.NavigateToAsync("MainPage");
    }

    /// <summary>
    /// Loads user data based on the current UserId.
    /// For UserId = 0, initializes new user creation mode.
    /// For UserId > 0, loads existing user data from database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user doesn't have admin role</exception>
    private async Task LoadUserAsync()
    {
        if (!_authService.HasRole(RoleConstants.Admin))
        {
            await _navigationService.NavigateToAsync("MainPage");
            return;
        }

        IsLoading = true;
        try
        {
            // Load all roles first
            var allRoles = await _context.Roles.ToListAsync();
            
            if (UserId == 0)
            {
                // New user
                IsNewUser = true;
                _currentUser = null;
                FirstName = string.Empty;
                LastName = string.Empty;
                Email = string.Empty;
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                IsActive = true;

                AvailableRoles = new ObservableCollection<RoleItem>(
                    allRoles.Select(r => new RoleItem 
                    { 
                        Id = r.Id, 
                        Name = r.Name, 
                        Description = r.Description, 
                        IsAssigned = false 
                    }));
            }
            else
            {
                // Existing user
                IsNewUser = false;
                _currentUser = await _context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == UserId);

                if (_currentUser == null)
                {
                    ErrorMessage = "User not found";
                    return;
                }

                FirstName = _currentUser.FirstName;
                LastName = _currentUser.LastName;
                Email = _currentUser.Email;
                IsActive = _currentUser.IsActive;

                var userRoleIds = _currentUser.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.RoleId).ToList();
                
                AvailableRoles = new ObservableCollection<RoleItem>(
                    allRoles.Select(r => new RoleItem 
                    { 
                        Id = r.Id, 
                        Name = r.Name, 
                        Description = r.Description, 
                        IsAssigned = userRoleIds.Contains(r.Id) 
                    }));
            }

            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(ShowPasswordFields));
            OnPropertyChanged(nameof(CanDeleteCurrentUser));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Determines whether the save user command can be executed.
    /// </summary>
    /// <returns>True if the save command can be executed, false otherwise</returns>
    /// <remarks>
    /// Validates that required fields are filled and for new users, password fields are completed.
    /// </remarks>
    private bool CanSaveUser()
    {
        return !IsLoading && 
               !string.IsNullOrWhiteSpace(FirstName) &&
               !string.IsNullOrWhiteSpace(LastName) &&
               !string.IsNullOrWhiteSpace(Email) &&
               (!IsNewUser || (!string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ConfirmPassword)));
    }

    /// <summary>
    /// Saves the user data (creates new user or updates existing user).
    /// </summary>
    /// <returns>A task representing the asynchronous save operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails or duplicate email exists</exception>
    private async Task SaveUserAsync()
    {
        ClearMessages();

        if (!ValidateInput())
            return;

        IsLoading = true;
        try
        {
            if (IsNewUser)
            {
                await CreateUserAsync();
            }
            else
            {
                await UpdateUserAsync();
            }

            SuccessMessage = IsNewUser ? "User created successfully!" : "User updated successfully!";
            
            if (IsNewUser)
            {
                await Task.Delay(1500);
                await NavigateBackAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Creates a new user in the database with the specified details and roles.
    /// </summary>
    /// <returns>A task representing the asynchronous create operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when a user with the same email already exists</exception>
    private async Task CreateUserAsync()
    {
        // Check if user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email.Trim());
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        // Create password hash
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(Password, salt);

        // Create new user
        var user = new User
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            Email = Email.Trim(),
            PasswordHash = hashedPassword,
            PasswordSalt = salt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = IsActive
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assign selected roles
        var selectedRoles = AvailableRoles.Where(r => r.IsAssigned).ToList();
        foreach (var role in selectedRoles)
        {
            var userRole = new UserRole(user.Id, role.Id);
            _context.UserRoles.Add(userRole);
        }

        if (selectedRoles.Any())
        {
            await _context.SaveChangesAsync();
        }

        _currentUser = user;
        IsNewUser = false;
    }

    /// <summary>
    /// Updates an existing user's information in the database.
    /// </summary>
    /// <returns>A task representing the asynchronous update operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when email is already used by another user</exception>
    private async Task UpdateUserAsync()
    {
        if (_currentUser == null) return;

        // Check if email is already used by another user
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == Email.Trim() && u.Id != _currentUser.Id);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email is already used by another user");
        }

        _currentUser.FirstName = FirstName.Trim();
        _currentUser.LastName = LastName.Trim();
        _currentUser.Email = Email.Trim();
        _currentUser.IsActive = IsActive;
        _currentUser.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(_currentUser);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Determines whether the delete user command can be executed.
    /// </summary>
    /// <returns>True if the delete command can be executed, false otherwise</returns>
    private bool CanDeleteUser()
    {
        return !IsLoading && !IsNewUser && CanDeleteCurrentUser;
    }

    /// <summary>
    /// Performs a soft delete of the current user after confirmation.
    /// </summary>
    /// <returns>A task representing the asynchronous delete operation</returns>
    /// <remarks>
    /// This method performs a soft delete by setting IsActive to false and DeletedAt timestamp.
    /// It also deactivates all associated user roles.
    /// </remarks>
    private async Task DeleteUserAsync()
    {
        if (_currentUser == null) return;

        var result = await Application.Current!.MainPage!.DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete user '{_currentUser.FullName}'? This action cannot be undone.",
            "Delete",
            "Cancel");

        if (!result) return;

        IsLoading = true;
        try
        {
            // Soft delete - mark as inactive and set deleted date
            _currentUser.IsActive = false;
            _currentUser.DeletedAt = DateTime.UtcNow;
            _currentUser.UpdatedAt = DateTime.UtcNow;

            // Also deactivate all user roles
            var userRoles = await _context.UserRoles.Where(ur => ur.UserId == _currentUser.Id).ToListAsync();
            foreach (var userRole in userRoles)
            {
                userRole.MarkAsDeleted();
            }

            _context.Users.Update(_currentUser);
            _context.UserRoles.UpdateRange(userRoles);
            await _context.SaveChangesAsync();

            await NavigateBackAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Adds a role to the current user.
    /// </summary>
    /// <param name="role">The role to add to the user</param>
    /// <returns>A task representing the asynchronous add role operation</returns>
    /// <remarks>
    /// Only works for existing users (not new user creation mode).
    /// </remarks>
    private async Task AddRoleAsync(RoleItem role)
    {
        if (_currentUser == null || role.IsAssigned) return;

        try
        {
            var userRole = new UserRole(_currentUser.Id, role.Id);
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            role.IsAssigned = true;
            SuccessMessage = $"Role '{role.Name}' added successfully!";
            await Task.Delay(1500);
            ClearMessages();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error adding role: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes a role from the current user.
    /// </summary>
    /// <param name="role">The role to remove from the user</param>
    /// <returns>A task representing the asynchronous remove role operation</returns>
    /// <remarks>
    /// Performs a soft delete by marking the user role as deleted rather than physically removing it.
    /// </remarks>
    private async Task RemoveRoleAsync(RoleItem role)
    {
        if (_currentUser == null || !role.IsAssigned) return;

        try
        {
            var userRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == _currentUser.Id && ur.RoleId == role.Id && ur.IsActive);

            if (userRole != null)
            {
                userRole.MarkAsDeleted();
                _context.UserRoles.Update(userRole);
                await _context.SaveChangesAsync();

                role.IsAssigned = false;
                SuccessMessage = $"Role '{role.Name}' removed successfully!";
                await Task.Delay(1500);
                ClearMessages();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error removing role: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates all user input fields.
    /// </summary>
    /// <returns>True if all validation passes, false otherwise</returns>
    /// <remarks>
    /// Validates required fields, email format, and password requirements for new users.
    /// Sets appropriate error messages when validation fails.
    /// </remarks>
    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            ErrorMessage = "First name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "Last name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email is required.";
            return false;
        }

        if (!IsValidEmail(Email.Trim()))
        {
            ErrorMessage = "Please enter a valid email address.";
            return false;
        }

        if (IsNewUser)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Password is required.";
                return false;
            }

            if (Password.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters long.";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates email address format using regular expression.
    /// </summary>
    /// <param name="email">The email address to validate</param>
    /// <returns>True if email format is valid, false otherwise</returns>
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, emailPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Navigates back to the user list page.
    /// </summary>
    /// <returns>A task representing the asynchronous navigation operation</returns>
    private async Task NavigateBackAsync()
    {
        await _navigationService.NavigateToAsync("UserListPage");
    }

    /// <summary>
    /// Clears both error and success messages.
    /// </summary>
    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }

    #endregion

    #region INotifyPropertyChanged

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed</param>
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Represents a role item in the user interface with assignment state.
/// </summary>
/// <remarks>
/// This class is used to display roles in the UI with the ability to track
/// whether the role is assigned to the current user and provide appropriate
/// button text and colors for add/remove actions.
/// </remarks>
public class RoleItem : INotifyPropertyChanged
{
    /// <summary>Flag indicating whether this role is assigned to the current user</summary>
    private bool _isAssigned;

    /// <summary>
    /// Gets or sets the unique identifier for the role.
    /// </summary>
    /// <value>The role ID</value>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    /// <value>The role name</value>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the description of the role.
    /// </summary>
    /// <value>The role description</value>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether this role is assigned to the current user.
    /// </summary>
    /// <value>True if the role is assigned, false otherwise</value>
    /// <remarks>
    /// Setting this property also updates the ButtonText and ButtonColor properties.
    /// </remarks>
    public bool IsAssigned
    {
        get => _isAssigned;
        set
        {
            _isAssigned = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ButtonText));
            OnPropertyChanged(nameof(ButtonColor));
        }
    }

    /// <summary>
    /// Gets the text to display on the action button.
    /// </summary>
    /// <value>"Remove" if assigned, "Add" if not assigned</value>
    public string ButtonText => IsAssigned ? "Remove" : "Add";
    
    /// <summary>
    /// Gets the color for the action button.
    /// </summary>
    /// <value>Red (#dc3545) if assigned, Green (#28a745) if not assigned</value>
    public Color ButtonColor => IsAssigned ? Color.FromArgb("#dc3545") : Color.FromArgb("#28a745");

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed</param>
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}