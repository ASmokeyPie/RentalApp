using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;

namespace RentalApp.Tests.Support;

/// <summary>
/// xUnit class fixture pointing at the Docker Compose PostgreSQL instance.
/// Applies pending migrations once on startup; exposes <see cref="ResetAsync"/>
/// to truncate all user-data tables between tests.
///
/// Prerequisites: <c>docker compose up -d db</c> before running (locally), or the
/// CI service container (GitHub Actions). The connection string is read from the
/// <c>TEST_CONNECTION_STRING</c> environment variable. If unset, falls back to the
/// standard local dev defaults that match <c>.env.example</c>.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
        ?? "Host=localhost;Username=app_user;Password=app_password;Database=appdb";

    public IDbContextFactory<AppDbContext> Factory { get; } =
        new TestDbContextFactory(ConnectionString);

    public async Task InitializeAsync()
    {
        using var db = Factory.CreateDbContext();
        // Apply any pending migrations — idempotent when already up-to-date.
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Truncates all writable tables in FK-safe order. Seeded categories are
    /// never touched. Call this at the start of each test that writes data.
    /// </summary>
    public async Task ResetAsync()
    {
        using var db = Factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE reviews, rentals, items, users RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- Private factory --------------------------------------------------

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(string connectionString)
        {
            var b = new DbContextOptionsBuilder<AppDbContext>();
            b.UseNpgsql(connectionString, o => o.UseNetTopologySuite());
            _options = b.Options;
        }

        public AppDbContext CreateDbContext() => new(_options);
    }
}
