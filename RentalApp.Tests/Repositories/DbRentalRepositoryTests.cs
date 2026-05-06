using NetTopologySuite.Geometries;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories.Db;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="DbRentalRepository"/> against the real
/// PostgreSQL instance. Requires: docker compose up -d db.
/// </summary>
[Collection("Database")]
public sealed class DbRentalRepositoryTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly StubCurrentUserProvider _user = new();
    private readonly DbRentalRepository _repo;

    public DbRentalRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repo = new DbRentalRepository(_fixture.Factory, _user);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync()    => Task.CompletedTask;

    // ---- RequestAsync -----------------------------------------------------

    [Fact]
    public async Task RequestAsync_Creates_Rental_With_Correct_TotalPrice()
    {
        // Arrange
        var (owner, borrower, item) = await SeedScenarioAsync();
        _user.CurrentUserId = borrower.Id;

        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var end   = start.AddDays(3); // 3-day rental at £10/day

        // Act
        var rental = await _repo.RequestAsync(item.Id, start, end);

        // Assert
        Assert.True(rental.Id > 0);
        Assert.Equal(RentalStatus.Requested, rental.Status);
        Assert.Equal(30.00m, rental.TotalPrice);
        Assert.Equal(borrower.Id, rental.BorrowerId);
        Assert.Equal(item.Title, rental.ItemTitle);
    }

    [Fact]
    public async Task RequestAsync_Throws_WhenEndDateNotAfterStartDate()
    {
        // Arrange
        var (_, borrower, item) = await SeedScenarioAsync();
        _user.CurrentUserId = borrower.Id;

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repo.RequestAsync(item.Id, startDate: date, endDate: date));
    }

    // ---- GetIncomingAsync -------------------------------------------------

    [Fact]
    public async Task GetIncomingAsync_Returns_Only_Rentals_On_Owners_Items()
    {
        // Arrange
        var (owner, borrower, item) = await SeedScenarioAsync();
        var unrelatedOwner = await SeedUserAsync("other@test.com");
        var unrelatedItem  = await SeedItemAsync(unrelatedOwner.Id);
        var unrelatedRenter = await SeedUserAsync("renter2@test.com");

        // Rental on owner's item
        await SeedRentalAsync(item.Id, borrower.Id);
        // Rental on someone else's item — should NOT appear in owner's incoming
        await SeedRentalAsync(unrelatedItem.Id, unrelatedRenter.Id);

        _user.CurrentUserId = owner.Id;

        // Act
        var incoming = await _repo.GetIncomingAsync();

        // Assert
        Assert.Single(incoming);
        Assert.Equal(item.Id, incoming[0].ItemId);
    }

    [Fact]
    public async Task GetIncomingAsync_Filters_By_Status()
    {
        // Arrange
        var (owner, borrower, item) = await SeedScenarioAsync();
        await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.Requested);
        await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.Approved);

        _user.CurrentUserId = owner.Id;

        // Act
        var approved = await _repo.GetIncomingAsync(new RentalQuery { Status = RentalStatus.Approved });

        // Assert
        Assert.Single(approved);
        Assert.Equal(RentalStatus.Approved, approved[0].Status);
    }

    // ---- GetOutgoingAsync -------------------------------------------------

    [Fact]
    public async Task GetOutgoingAsync_Returns_Only_Current_Users_Rentals()
    {
        // Arrange
        var (_, borrower1, item) = await SeedScenarioAsync();
        var borrower2 = await SeedUserAsync("b2@test.com");

        await SeedRentalAsync(item.Id, borrower1.Id);
        await SeedRentalAsync(item.Id, borrower2.Id);

        _user.CurrentUserId = borrower1.Id;

        // Act
        var outgoing = await _repo.GetOutgoingAsync();

        // Assert
        Assert.Single(outgoing);
        Assert.Equal(borrower1.Id, outgoing[0].BorrowerId);
    }

    // ---- UpdateStatusAsync ------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_Persists_NewStatus()
    {
        // Arrange
        var (owner, borrower, item) = await SeedScenarioAsync();
        var rental = await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.Requested);

        _user.CurrentUserId = owner.Id;

        // Act
        var update = await _repo.UpdateStatusAsync(rental.Id, RentalStatus.Approved);

        // Assert
        Assert.Equal(RentalStatus.Approved, update.Status);

        using var db = _fixture.Factory.CreateDbContext();
        var reloaded = await db.Rentals.FindAsync(rental.Id);
        Assert.Equal(RentalStatus.Approved, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_Maps_Overdue_To_OutForRent_In_Database()
    {
        // Overdue is a client-side derived state — the DB must never store it.
        // Arrange
        var (owner, borrower, item) = await SeedScenarioAsync();
        var rental = await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.OutForRent);

        _user.CurrentUserId = owner.Id;

        // Act
        await _repo.UpdateStatusAsync(rental.Id, RentalStatus.Overdue);

        // Assert
        using var db = _fixture.Factory.CreateDbContext();
        var reloaded = await db.Rentals.FindAsync(rental.Id);
        // The persisted value must be OutForRent, not Overdue.
        Assert.Equal(RentalStatus.OutForRent, reloaded!.Status);
    }

    // ---- DeriveStatus / Overdue -------------------------------------------

    [Fact]
    public async Task GetOutgoingAsync_Elevates_OutForRent_To_Overdue_WhenEndDatePast()
    {
        // Arrange
        var (_, borrower, item) = await SeedScenarioAsync();

        // Rental with end date in the past and status OutForRent.
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.OutForRent,
            startDate: pastEnd.AddDays(-3), endDate: pastEnd);

        _user.CurrentUserId = borrower.Id;

        // Act
        var outgoing = await _repo.GetOutgoingAsync();

        // Assert
        Assert.Single(outgoing);
        Assert.Equal(RentalStatus.Overdue, outgoing[0].Status);
    }

    [Fact]
    public async Task GetIncomingAsync_Overdue_Filter_Matches_OutForRent_With_PastEndDate()
    {
        // Arrange
        var (owner, borrower, item) = await SeedScenarioAsync();

        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.OutForRent,
            startDate: pastEnd.AddDays(-2), endDate: pastEnd);

        // A current OutForRent that is NOT overdue.
        var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        await SeedRentalAsync(item.Id, borrower.Id, RentalStatus.OutForRent,
            startDate: futureEnd.AddDays(-1), endDate: futureEnd);

        _user.CurrentUserId = owner.Id;

        // Act
        var overdueOnly = await _repo.GetIncomingAsync(new RentalQuery { Status = RentalStatus.Overdue });

        // Assert
        Assert.Single(overdueOnly);
        Assert.Equal(RentalStatus.Overdue, overdueOnly[0].Status);
    }

    // ---- Seed helpers -----------------------------------------------------

    private async Task<(User owner, User borrower, Item item)> SeedScenarioAsync()
    {
        var owner    = await SeedUserAsync("owner@test.com",    "Alice", "Smith");
        var borrower = await SeedUserAsync("borrower@test.com", "Bob",   "Jones");
        var item     = await SeedItemAsync(owner.Id);
        return (owner, borrower, item);
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

    private async Task<Rental> SeedRentalAsync(
        int          itemId,
        int          borrowerId,
        RentalStatus status    = RentalStatus.Requested,
        DateOnly?    startDate = null,
        DateOnly?    endDate   = null)
    {
        using var db = _fixture.Factory.CreateDbContext();
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var end   = endDate   ?? start.AddDays(3);
        var rental = new Rental
        {
            ItemId     = itemId,
            BorrowerId = borrowerId,
            StartDate  = start,
            EndDate    = end,
            Status     = status,
            TotalPrice = 30m,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };
        db.Rentals.Add(rental);
        await db.SaveChangesAsync();
        return rental;
    }
}
