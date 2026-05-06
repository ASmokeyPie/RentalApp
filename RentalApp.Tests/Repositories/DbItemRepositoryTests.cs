using NetTopologySuite.Geometries;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories.Db;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="DbItemRepository"/> against the real
/// PostgreSQL instance. Requires: docker compose up -d db.
/// </summary>
[Collection("Database")]
public sealed class DbItemRepositoryTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly StubCurrentUserProvider _user = new();
    private readonly DbItemRepository _repo;

    public DbItemRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repo = new DbItemRepository(_fixture.Factory, _user);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync()    => Task.CompletedTask;

    // ---- CreateAsync ------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Persists_Item_And_Assigns_Id()
    {
        // Arrange
        var owner = await SeedUserAsync("owner@test.com");
        _user.CurrentUserId = owner.Id;

        // Act
        var created = await _repo.CreateAsync(new Item
        {
            Title      = "Cordless Drill",
            DailyRate  = 5.00m,
            CategoryId = 1,             // Power Tools
            Latitude   = 55.9533,
            Longitude  = -3.1883,
        });

        // Assert
        Assert.True(created.Id > 0);
        Assert.Equal("Cordless Drill", created.Title);
        Assert.Equal(owner.Id, created.OwnerId);
    }

    [Fact]
    public async Task CreateAsync_Sets_Location_Column()
    {
        // Arrange
        var owner = await SeedUserAsync("owner2@test.com");
        _user.CurrentUserId = owner.Id;

        // Act
        var created = await _repo.CreateAsync(new Item
        {
            Title = "Tent", DailyRate = 10m, CategoryId = 3,
            Latitude = 55.9533, Longitude = -3.1883,
        });

        // Reload raw from DB to confirm Location was written.
        // Assert
        using var db = _fixture.Factory.CreateDbContext();
        var row = await db.Items.FindAsync(created.Id);
        Assert.NotNull(row!.Location);
        Assert.Equal(4326, row.Location!.SRID);
    }

    // ---- GetByIdAsync -----------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Returns_Item_With_DisplayFields()
    {
        // Arrange
        var owner = await SeedUserAsync("ada@test.com", "Ada", "Lovelace");
        var item  = await SeedItemAsync(owner.Id, title: "Hammer", categoryId: 2);

        // Act
        var loaded = await _repo.GetByIdAsync(item.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Hammer",      loaded!.Title);
        Assert.Equal("Ada L.",      loaded.OwnerName);
        Assert.Equal("Hand Tools",  loaded.CategoryName);
        Assert.Equal("hand-tools",  loaded.CategorySlug);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_WhenNotFound()
    {
        // Act
        var result = await _repo.GetByIdAsync(999_999);

        // Assert
        Assert.Null(result);
    }

    // ---- SearchAsync ------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Filters_By_CategorySlug()
    {
        // Arrange
        var owner = await SeedUserAsync("bob@test.com");
        await SeedItemAsync(owner.Id, title: "Drill",  categoryId: 1); // power-tools
        await SeedItemAsync(owner.Id, title: "Hammer", categoryId: 2); // hand-tools

        // Act
        var result = await _repo.SearchAsync(new ItemQuery { CategorySlug = "power-tools" });

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Drill", result.Items[0].Title);
    }

    [Fact]
    public async Task SearchAsync_Filters_By_SearchText_CaseInsensitive()
    {
        // Arrange
        var owner = await SeedUserAsync("carol@test.com");
        await SeedItemAsync(owner.Id, title: "Circular Saw", categoryId: 1);
        await SeedItemAsync(owner.Id, title: "Tent",         categoryId: 3);

        // Act
        var result = await _repo.SearchAsync(new ItemQuery { Search = "circular" });

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Circular Saw", result.Items[0].Title);
    }

    [Fact]
    public async Task SearchAsync_Returns_All_WhenNoFilters_And_Respects_Paging()
    {
        // Arrange
        var owner = await SeedUserAsync("dan@test.com");
        for (var i = 1; i <= 5; i++)
            await SeedItemAsync(owner.Id, title: $"Item {i}", categoryId: 1);

        // Act
        var page1 = await _repo.SearchAsync(new ItemQuery { Page = 1, PageSize = 3 });
        var page2 = await _repo.SearchAsync(new ItemQuery { Page = 2, PageSize = 3 });

        // Assert
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
    }

    // ---- UpdateAsync ------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Persists_Changes()
    {
        // Arrange
        var owner = await SeedUserAsync("eve@test.com");
        var item  = await SeedItemAsync(owner.Id, title: "Old Title", categoryId: 1);

        item.Title     = "New Title";
        item.DailyRate = 99.99m;

        // Act
        await _repo.UpdateAsync(item);

        // Assert
        using var db = _fixture.Factory.CreateDbContext();
        var reloaded = await db.Items.FindAsync(item.Id);
        Assert.Equal("New Title", reloaded!.Title);
        Assert.Equal(99.99m, reloaded.DailyRate);
    }

    // ---- DeleteAsync ------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Removes_Item()
    {
        // Arrange
        var owner = await SeedUserAsync("frank@test.com");
        var item  = await SeedItemAsync(owner.Id, title: "Doomed Item", categoryId: 1);

        // Act
        await _repo.DeleteAsync(item.Id);

        // Assert
        using var db = _fixture.Factory.CreateDbContext();
        Assert.Null(await db.Items.FindAsync(item.Id));
    }

    // ---- GetNearbyAsync ---------------------------------------------------

    [Fact]
    public async Task GetNearbyAsync_Returns_Items_Within_Radius_OrderedByDistance()
    {
        // Arrange
        var owner = await SeedUserAsync("geo@test.com");

        // Edinburgh city centre — all within 1 km.
        await SeedItemAsync(owner.Id, title: "Near",  lat: 55.9533, lon: -3.1883); // ~0 km
        await SeedItemAsync(owner.Id, title: "Medium",lat: 55.9550, lon: -3.1900); // ~0.2 km
        // Glasgow — far away, should be excluded.
        await SeedItemAsync(owner.Id, title: "Far",   lat: 55.8617, lon: -4.2583);

        // Act
        var results = await _repo.GetNearbyAsync(55.9533, -3.1883, radiusKm: 1.0);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Near", results[0].Title);
        Assert.All(results, r => Assert.NotNull(r.DistanceKm));
        Assert.True(results[0].DistanceKm <= results[1].DistanceKm);
    }

    // ---- Seed helpers -----------------------------------------------------

    private async Task<User> SeedUserAsync(
        string email,
        string firstName = "Test",
        string lastName  = "User")
    {
        using var db = _fixture.Factory.CreateDbContext();
        var user = new User
        {
            FirstName    = firstName,
            LastName     = lastName,
            Email        = email,
            PasswordHash = "dummy-hash",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Item> SeedItemAsync(
        int    ownerId,
        string title      = "Test Item",
        int    categoryId = 1,
        double lat        = 55.9533,
        double lon        = -3.1883)
    {
        using var db = _fixture.Factory.CreateDbContext();
        var item = new Item
        {
            Title      = title,
            DailyRate  = 10m,
            CategoryId = categoryId,
            OwnerId    = ownerId,
            Latitude   = lat,
            Longitude  = lon,
            Location   = new Point(lon, lat) { SRID = 4326 },
            IsAvailable = true,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }
}
