using RentalApp.Database.Services;

namespace RentalApp.Tests.Support;

/// <summary>
/// In-process <see cref="ICurrentUserProvider"/> for integration tests.
/// Set <see cref="CurrentUserId"/> in test setup to control which user
/// the repositories see as "authenticated".
/// </summary>
public sealed class StubCurrentUserProvider : ICurrentUserProvider
{
    public int? CurrentUserId { get; set; }
}
