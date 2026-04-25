using System.Net;
using System.Text.Json;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories.Api;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

public class ApiRentalRepositoryTests
{
    // ---- RequestAsync -----------------------------------------------------

    [Fact]
    public async Task RequestAsync_PostsRentalsWithExpectedBody()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 99,
            itemId = 7,
            itemTitle = "Drill",
            borrowerId = 3,
            borrowerName = "Bob",
            ownerId = 1,
            ownerName = "Ada",
            startDate = "2026-05-01",
            endDate = "2026-05-03",
            status = "Pending",
            totalPrice = 16.50m,
            createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        var rental = await repo.RequestAsync(7, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));

        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/rentals", request.RequestUri!.AbsolutePath);

        var bodyJson = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        var root = doc.RootElement;
        Assert.Equal(7, root.GetProperty("itemId").GetInt32());
        Assert.Equal("2026-05-01", root.GetProperty("startDate").GetString());
        Assert.Equal("2026-05-03", root.GetProperty("endDate").GetString());

        Assert.Equal(99, rental.Id);
        Assert.Equal(RentalStatus.Pending, rental.Status);
        Assert.Equal("Drill", rental.ItemTitle);
        Assert.Equal("Bob", rental.BorrowerName);
        Assert.Equal("Ada", rental.OwnerName);
    }

    // ---- Incoming / Outgoing ---------------------------------------------

    [Fact]
    public async Task GetIncomingAsync_HitsIncomingEndpoint_NoFilter()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(Array.Empty<object>()));
        var repo = BuildRepo(stub);

        await repo.GetIncomingAsync();

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/rentals/incoming", uri.AbsolutePath);
        Assert.Equal(string.Empty, uri.Query);
    }

    [Fact]
    public async Task GetIncomingAsync_AppendsStatusFilter_WhenProvided()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(Array.Empty<object>()));
        var repo = BuildRepo(stub);

        await repo.GetIncomingAsync(new RentalQuery { Status = RentalStatus.Approved });

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/rentals/incoming", uri.AbsolutePath);
        Assert.Equal("?status=Approved", uri.Query);
    }

    [Fact]
    public async Task GetOutgoingAsync_HitsOutgoingEndpoint()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(Array.Empty<object>()));
        var repo = BuildRepo(stub);

        await repo.GetOutgoingAsync();

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/rentals/outgoing", uri.AbsolutePath);
    }

    // ---- GetByIdAsync -----------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_On404()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.NotFound));
        var repo = BuildRepo(stub);

        var rental = await repo.GetByIdAsync(99);

        Assert.Null(rental);
    }

    [Fact]
    public async Task GetByIdAsync_Parses200()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            itemId = 1,
            borrowerId = 2,
            startDate = "2026-04-01",
            endDate = "2026-04-02",
            status = "Approved",
            totalPrice = 10m,
            createdAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        var rental = await repo.GetByIdAsync(7);

        Assert.NotNull(rental);
        Assert.Equal(RentalStatus.Approved, rental!.Status);
        Assert.Equal(new DateOnly(2026, 4, 1), rental.StartDate);
    }

    // ---- UpdateStatusAsync ------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_PatchesIdRoute_WithStatusBody()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            itemId = 1,
            borrowerId = 2,
            startDate = "2026-04-01",
            endDate = "2026-04-02",
            status = "Approved",
            totalPrice = 10m,
            createdAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        var rental = await repo.UpdateStatusAsync(7, RentalStatus.Approved);

        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/rentals/7/status", request.RequestUri!.AbsolutePath);

        var body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Approved", doc.RootElement.GetProperty("status").GetString());

        Assert.Equal(RentalStatus.Approved, rental.Status);
    }

    // ---- Generic CRUD methods that don't fit the API ---------------------

    [Fact]
    public async Task ListAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task CreateAsync_FromEntity_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => repo.CreateAsync(new Rental
            {
                ItemId = 1, BorrowerId = 2,
                StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 2),
            }));
    }

    [Fact]
    public async Task UpdateAsync_FromEntity_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.UpdateAsync(new Rental()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeleteAsync(1));
    }

    // ---- Helpers ----------------------------------------------------------

    private static ApiRentalRepository BuildRepo(StubHttpMessageHandler stub)
    {
        var client = new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") };
        return new ApiRentalRepository(client);
    }
}
