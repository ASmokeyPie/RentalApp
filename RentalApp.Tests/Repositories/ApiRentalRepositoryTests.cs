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
        // Spec: POST /rentals body is { itemId, startDate, endDate } (date strings),
        // response is the full rental shape with createdAt.
        // Arrange
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
            status = "Requested",
            totalPrice = 16.50m,
            createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        // Act
        var rental = await repo.RequestAsync(7, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));

        // Assert
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
        Assert.Equal(RentalStatus.Requested, rental.Status);
        Assert.Equal("Drill", rental.ItemTitle);
        Assert.Equal("Bob", rental.BorrowerName);
        Assert.Equal("Ada", rental.OwnerName);
        Assert.Equal(new DateOnly(2026, 5, 1), rental.StartDate);
    }

    // ---- Incoming / Outgoing ---------------------------------------------

    [Fact]
    public async Task GetIncomingAsync_HitsIncomingEndpoint_NoFilter()
    {
        // Spec: response wraps in { rentals, totalRentals }.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            rentals = Array.Empty<object>(),
            totalRentals = 0,
        }));
        var repo = BuildRepo(stub);

        // Act
        await repo.GetIncomingAsync();

        // Assert
        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/rentals/incoming", uri.AbsolutePath);
        Assert.Equal(string.Empty, uri.Query);
    }

    [Fact]
    public async Task GetIncomingAsync_AppendsStatusFilter_WhenProvided()
    {
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            rentals = Array.Empty<object>(),
            totalRentals = 0,
        }));
        var repo = BuildRepo(stub);

        // Act
        await repo.GetIncomingAsync(new RentalQuery { Status = RentalStatus.Approved });

        // Assert
        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/rentals/incoming", uri.AbsolutePath);
        Assert.Equal("?status=Approved", uri.Query);
    }

    [Fact]
    public async Task GetOutgoingAsync_HitsOutgoingEndpoint_AndParsesEnvelope()
    {
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            rentals = new[]
            {
                new
                {
                    id = 1,
                    itemId = 7,
                    itemTitle = "Drill",
                    borrowerId = 3,
                    borrowerName = "Bob",
                    ownerId = 1,
                    ownerName = "Ada",
                    startDate = "2026-05-01",
                    endDate = "2026-05-03",
                    status = "Requested",
                    totalPrice = 10m,
                    createdAt = DateTime.UtcNow,
                },
            },
            totalRentals = 1,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.GetOutgoingAsync();

        // Assert
        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/rentals/outgoing", uri.AbsolutePath);
        Assert.Single(result);
        Assert.Equal("Drill", result[0].ItemTitle);
    }

    // ---- GetByIdAsync -----------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_On404()
    {
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.NotFound));
        var repo = BuildRepo(stub);

        // Act
        var rental = await repo.GetByIdAsync(99);

        // Assert
        Assert.Null(rental);
    }

    [Theory]
    [InlineData("2026-04-01", "2026-04-02")]                                           // spec-stated yyyy-MM-dd
    [InlineData("2026-04-01T00:00:00.000Z", "2026-04-02T00:00:00.000Z")]              // ISO 8601 with millis + Z (what the live API actually emits)
    [InlineData("2026-04-01T00:00:00Z", "2026-04-02T00:00:00Z")]                      // ISO 8601 without millis
    public async Task GetByIdAsync_TolerantOfDateFormats(string startDate, string endDate)
    {
        // Regression: live API sends full ISO datetimes for startDate/endDate
        // even though the spec types them as "yyyy-MM-dd". The repo's parser
        // handles both shapes plus a prefix fallback.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            itemId = 1,
            itemTitle = "Drill",
            borrowerId = 2,
            borrowerName = "Bob",
            ownerId = 1,
            ownerName = "Ada",
            startDate,
            endDate,
            status = "Approved",
            totalPrice = 10m,
            requestedAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        // Act
        var rental = await repo.GetByIdAsync(7);

        // Assert
        Assert.NotNull(rental);
        Assert.Equal(new DateOnly(2026, 4, 1), rental!.StartDate);
        Assert.Equal(new DateOnly(2026, 4, 2), rental.EndDate);
    }

    [Fact]
    public async Task GetByIdAsync_Parses200_WithRequestedAtAndItemDescription()
    {
        // Spec: GET /rentals/{id} uses 'requestedAt' (not createdAt)
        // and includes 'itemDescription'.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            itemId = 1,
            itemTitle = "Drill",
            itemDescription = "An 18V drill",
            borrowerId = 2,
            borrowerName = "Bob",
            ownerId = 1,
            ownerName = "Ada",
            startDate = "2026-04-01",
            endDate = "2026-04-02",
            status = "Approved",
            totalPrice = 10m,
            requestedAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        // Act
        var rental = await repo.GetByIdAsync(7);

        // Assert
        Assert.NotNull(rental);
        Assert.Equal(RentalStatus.Approved, rental!.Status);
        Assert.Equal(new DateOnly(2026, 4, 1), rental.StartDate);
        Assert.Equal("Drill", rental.ItemTitle);
    }

    // ---- UpdateStatusAsync ------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_PatchesIdRoute_AndReturnsSlimUpdate()
    {
        // Spec: PATCH /rentals/{id}/status returns { id, status, updatedAt }.
        // The repo returns a RentalStatusUpdate (not a full Rental) to match.
        // Arrange
        var updatedAt = DateTime.UtcNow;
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            status = "Approved",
            updatedAt = updatedAt,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.UpdateStatusAsync(7, RentalStatus.Approved);

        // Assert
        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/rentals/7/status", request.RequestUri!.AbsolutePath);

        var body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Approved", doc.RootElement.GetProperty("status").GetString());

        Assert.Equal(7, result.Id);
        Assert.Equal(RentalStatus.Approved, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_SendsSpacedWireString_ForOutForRent()
    {
        // Regression: server rejected "OutForRent" with 409. The requirements
        // doc and (presumably) server use "Out for Rent" with spaces. We must
        // translate the enum on the way out.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            status = "Out for Rent",
            updatedAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.UpdateStatusAsync(7, RentalStatus.OutForRent);

        // Assert
        var body = await stub.Requests.Single().Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Out for Rent", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(RentalStatus.OutForRent, result.Status);
    }

    [Theory]
    [InlineData("Out for Rent")]   // requirements wording (canonical)
    [InlineData("Out For Rent")]   // full title case
    [InlineData("OutForRent")]     // CamelCase (our enum name)
    [InlineData("out_for_rent")]   // snake_case
    public async Task ParseStatus_TolerantOfOutForRentWireVariants(string wireStatus)
    {
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            status = wireStatus,
            updatedAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.UpdateStatusAsync(7, RentalStatus.OutForRent);

        // Assert
        Assert.Equal(RentalStatus.OutForRent, result.Status);
    }

    [Fact]
    public async Task GetIncomingAsync_StatusFilter_UsesWireString()
    {
        // The status= query parameter must use the same wire string as
        // PATCH body, otherwise the server's filter won't match anything.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            rentals = Array.Empty<object>(),
            totalRentals = 0,
        }));
        var repo = BuildRepo(stub);

        // Act
        await repo.GetIncomingAsync(new RentalQuery { Status = RentalStatus.OutForRent });

        // Assert
        var uri = stub.Requests.Single().RequestUri!;
        // URL-encoded spaces become %20.
        Assert.Equal("?status=Out%20for%20Rent", uri.Query);
    }

    [Fact]
    public async Task UpdateStatusAsync_SurfacesServerErrorMessage_On409()
    {
        // Regression: live API was returning 409 with a meaningful body
        // (e.g. "Rental cannot be marked OutForRent past its end date") but the
        // repo was throwing a generic "status code does not indicate success"
        // exception. The helper should now propagate the server's 'message'.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(
            new { error = "Conflict", message = "End date has already passed." },
            HttpStatusCode.Conflict));
        var repo = BuildRepo(stub);

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => repo.UpdateStatusAsync(7, RentalStatus.OutForRent));

        // Assert
        Assert.Equal("End date has already passed.", ex.Message);
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task ParseStatus_ThrowsInvalidData_OnUnknownWireValue()
    {
        // Defensive parser: any wire status outside the enum throws so a
        // wire-vs-enum drift isn't silently swallowed.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            status = "Acquired",   // not in our enum
            updatedAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => repo.UpdateStatusAsync(7, RentalStatus.Approved));
    }

    // ---- Generic CRUD methods that don't fit the API ---------------------

    [Fact]
    public async Task ListAsync_Throws_NotSupported()
    {
        // Arrange
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        // Act + Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task CreateAsync_FromEntity_Throws_NotSupported()
    {
        // Arrange
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        // Act + Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => repo.CreateAsync(new Rental
            {
                ItemId = 1,
                BorrowerId = 2,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 1, 2),
            }));
    }

    [Fact]
    public async Task UpdateAsync_FromEntity_Throws_NotSupported()
    {
        // Arrange
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        // Act + Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.UpdateAsync(new Rental()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_NotSupported()
    {
        // Arrange
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        // Act + Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeleteAsync(1));
    }

    // ---- Helpers ----------------------------------------------------------

    private static ApiRentalRepository BuildRepo(StubHttpMessageHandler stub)
    {
        // Arrange: repository under test with a stubbed HttpClient.
        var client = new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") };
        return new ApiRentalRepository(client);
    }
}
