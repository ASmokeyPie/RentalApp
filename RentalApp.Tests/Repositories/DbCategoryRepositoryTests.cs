using RentalApp.Database.Repositories.Db;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="DbCategoryRepository"/> against the real
/// PostgreSQL instance. Requires: docker compose up -d db.
/// </summary>
[Collection("Database")]
public sealed class DbCategoryRepositoryTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly DbCategoryRepository _repo;

    public DbCategoryRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repo = new DbCategoryRepository(_fixture.Factory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync()    => Task.CompletedTask;

    // ---- ListAsync --------------------------------------------------------

    [Fact]
    public async Task ListAsync_Returns_AllTwelveSeededCategories()
    {
        var categories = await _repo.ListAsync();

        Assert.Equal(12, categories.Count);
    }

    [Fact]
    public async Task ListAsync_OrderedById_FirstIsPowerTools()
    {
        var categories = await _repo.ListAsync();
        var first = categories.MinBy(c => c.Id);

        Assert.Equal("Power Tools", first!.Name);
        Assert.Equal("power-tools", first.Slug);
    }

    // ---- GetByIdAsync -----------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Returns_Category_WhenExists()
    {
        var category = await _repo.GetByIdAsync(3); // "Camping Gear"

        Assert.NotNull(category);
        Assert.Equal("Camping Gear", category!.Name);
        Assert.Equal("camping-gear", category.Slug);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_WhenNotFound()
    {
        var category = await _repo.GetByIdAsync(9999);

        Assert.Null(category);
    }
}
