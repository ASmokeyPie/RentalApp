using RentalApp.Database.Services;

namespace RentalApp.Services;

/// <summary>
/// Adapts <see cref="IAuthenticationService"/> to the
/// <see cref="ICurrentUserProvider"/> interface so that components in
/// <c>RentalApp.Database</c> can obtain the current user's ID without taking
/// a circular project dependency on <c>RentalApp</c>.
/// </summary>
internal sealed class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IAuthenticationService _auth;

    public CurrentUserProvider(IAuthenticationService auth) => _auth = auth;

    public int? CurrentUserId => _auth.CurrentUser?.Id;
}
