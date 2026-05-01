using NetTopologySuite.Geometries;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories.Db;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="DbReviewRepository"/> against the real
/// PostgreSQL instance. Requires: docker compose up -d db.
/// </summary>
[Collection("Database")]
public sealed class DbReviewRepositoryTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly StubCurrentUserProvider _user = new();
    private readonly DbReviewRepository _repo;

    public DbReviewRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repo = new DbReviewRepository(_fixture.Factory, _user);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync()    => Task.CompletedTask;

    // ---- CreateAsync ------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Persists_Review_And_Assigns_Id()
    {
        var (_, borrower, _, rental) = await SeedScenarioAsync();
        _user.CurrentUserId = borrower.Id;

        var review = await _repo.CreateAsync(rental.Id, rating: 5, comment: "Great!");

        Assert.True(review.Id > 0);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Great!", review.Comment);
        Assert.Equal(borrower.Id, review.ReviewerId);
    }

    [Fact]
    public async Task CreateAsync_Populates_DisplayFields()
    {
        var (owner, borrower, item, rental) = await SeedScenarioAsync("Ada", "Lovelace");
        _user.CurrentUserId = borrower.Id;

        var review = await _repo.CreateAsync(rental.Id, rating: 4, comment: null);

        Assert.Equal(item.Title, review.ItemTitle);
        Assert.Equal("Bob J.", review.ReviewerName);
    }

    [Fact]
    public async Task CreateAsync_NullComment_Is_Allowed()
    {
        var (_, borrower, _, rental) = await SeedScenarioAsync();
        _user.CurrentUserId = borrower.Id;

        var review = await _repo.CreateAsync(rental.Id, rating: 3, comment: null);

        Assert.Null(review.Comment);
    }

    // ---- GetForItemAsync --------------------------------------------------

    [Fact]
    public async Task GetForItemAsync_Returns_Reviews_For_Item()
    {
        var (owner1, borrower1, item1, rental1) = await SeedScenarioAsync();
        var (owner2, borrower2, item2, rental2) = await SeedScenarioAsync(
            ownerEmail: "o2@test.com", borrowerEmail: "b2@test.com");

        await SeedReviewAsync(rental1.Id, borrower1.Id, rating: 5);
        await SeedReviewAsync(rental2.Id, borrower2.Id, rating: 3);

        var result = await _repo.GetForItemAsync(item1.Id);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(5, result.Items[0].Rating);
    }

    [Fact]
    public async Task GetForItemAsync_Respects_Pagination()
    {
        var (owner, borrower, item, _) = await SeedScenarioAsync();

        // Seed 3 rentals → 3 reviews for the same item.
        for (var i = 1; i <= 3; i++)
        {
            var extra = await SeedUserAsync($"extra{i}@test.com");
            var rental = await SeedRentalAsync(item.Id, extra.Id);
            await SeedReviewAsync(rental.Id, extra.Id, rating: i);
        }

        var page1 = await _repo.GetForItemAsync(item.Id, page: 1, pageSize: 2);
        var page2 = await _repo.GetForItemAsync(item.Id, page: 2, pageSize: 2);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
    }

    // ---- GetForUserAsync --------------------------------------------------

    [Fact]
    public async Task GetForUserAsync_Returns_Reviews_Written_By_User()
    {
        var (_, borrower1, _, rental1) = await SeedScenarioAsync();
        var (_, borrower2, _, rental2) = await SeedScenarioAsync(
            ownerEmail: "o2@test.com", borrowerEmail: "b2@test.com");

        await SeedReviewAsync(rental1.Id, borrower1.Id, rating: 4, comment: "Good kit");
        await SeedReviewAsync(rental2.Id, borrower2.Id, rating: 2);

        var result = await _repo.GetForUserAsync(borrower1.Id);

        Assert.Single(result.Items);
        Assert.Equal("Good kit", result.Items[0].Comment);
    }

    [Fact]
    public async Task GetForUserAsync_Populates_ItemTitle()
    {
        var (_, borrower, item, rental) = await SeedScenarioAsync();
        await SeedReviewAsync(rental.Id, borrower.Id, rating: 5);

        var result = await _repo.GetForUserAsync(borrower.Id);

        Assert.Single(result.Items);
        Assert.Equal(item.Title, result.Items[0].ItemTitle);
    }

    // ---- Seed helpers -----------------------------------------------------

    private async Task<(User owner, User borrower, Item item, Rental rental)> SeedScenarioAsync(
        string ownerFirstName = "Alice",
        string ownerLastName  = "Smith",
        string ownerEmail     = "owner@test.com",
        string borrowerEmail  = "borrower@test.com")
    {
        var owner    = await SeedUserAsync(ownerEmail,    ownerFirstName, ownerLastName);
        var borrower = await SeedUserAsync(borrowerEmail, "Bob", "Jones");
        var item     = await SeedItemAsync(owner.Id);
        var rental   = await SeedRentalAsync(item.Id, borrower.Id);
        return (owner, borrower, item, rental);
    }

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

    private async Task<Item> SeedItemAsync(int ownerId)
    {
        using var db = _fixture.Factory.CreateDbContext();
        var item = new Item
        {
            Title      = "Test Item",
            DailyRate  = 10m,
            CategoryId = 1,
            OwnerId    = ownerId,
            Latitude   = 55.9533,
            Longitude  = -3.1883,
            Location   = new Point(-3.1883, 55.9533) { SRID = 4326 },
            IsAvailable = true,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private async Task<Rental> SeedRentalAsync(int itemId, int borrowerId)
    {
        using var db = _fixture.Factory.CreateDbContext();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        var rental = new Rental
        {
            ItemId     = itemId,
            BorrowerId = borrowerId,
            StartDate  = start,
            EndDate    = start.AddDays(3),
            Status     = RentalStatus.Completed,
            TotalPrice = 30m,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };
        db.Rentals.Add(rental);
        await db.SaveChangesAsync();
        return rental;
    }

    private async Task<Review> SeedReviewAsync(
        int     rentalId,
        int     reviewerId,
        int     rating  = 5,
        string? comment = null)
    {
        using var db = _fixture.Factory.CreateDbContext();
        var review = new Review
        {
            RentalId   = rentalId,
            ReviewerId = reviewerId,
            Rating     = rating,
            Comment    = comment,
            CreatedAt  = DateTime.UtcNow,
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }
}
