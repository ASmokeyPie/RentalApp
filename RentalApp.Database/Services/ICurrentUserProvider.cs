namespace RentalApp.Database.Services;

/// <summary>
/// Provides the ID of the currently authenticated user to components inside
/// <c>RentalApp.Database</c> that cannot take a dependency on the higher-level
/// <c>IAuthenticationService</c> without creating a circular project reference.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>
    /// The ID of the signed-in user, or <c>null</c> if no user is authenticated.
    /// </summary>
    int? CurrentUserId { get; }
}
