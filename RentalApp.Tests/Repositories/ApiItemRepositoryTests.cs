using System.Net;
using System.Text.Json;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories.Api;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

public class ApiItemRepositoryTests
{
    // ---- GetByIdAsync -----------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_On404()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.NotFound));
        var repo = BuildRepo(stub);

        var item = await repo.GetByIdAsync(42);

        Assert.Null(item);
        Assert.Equal("/items/42", stub.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_ParsesFullDetail_On200()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 42,
            title = "Cordless Drill",
            description = "An 18V drill",
            dailyRate = 5.50m,
            categoryId = 1,
            category = "Power Tools",
            categorySlug = "power-tools",
            ownerId = 7,
            ownerName = "Ada L.",
            ownerRating = 4.7,
            lat = 55.95,
            lon = -3.19,
            isAvailable = true,
            averageRating = 4.5,
            createdAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        var item = await repo.GetByIdAsync(42);

        Assert.NotNull(item);
        Assert.Equal(42, item!.Id);
        Assert.Equal("Cordless Drill", item.Title);
        Assert.Equal(5.50m, item.DailyRate);
        Assert.Equal(55.95, item.Latitude);
        Assert.Equal(-3.19, item.Longitude);
        Assert.Equal("Ada L.", item.OwnerName);
        Assert.Equal(4.7, item.OwnerRating);
        Assert.Equal(4.5, item.AverageRating);
        Assert.Equal("Power Tools", item.CategoryName);
        Assert.Equal("power-tools", item.CategorySlug);
    }

    // ---- SearchAsync (paginated GET /items) -------------------------------

    [Fact]
    public async Task SearchAsync_BuildsQueryString_AndParsesPagedEnvelope()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = new[]
            {
                new
                {
                    id = 1, title = "Drill", dailyRate = 5m,
                    categoryId = 1, ownerId = 7,
                    lat = 0.0, lon = 0.0, isAvailable = true,
                    createdAt = DateTime.UtcNow,
                },
            },
            page = 2,
            pageSize = 10,
            totalCount = 17,
        }));
        var repo = BuildRepo(stub);

        var result = await repo.SearchAsync(new ItemQuery
        {
            CategorySlug = "power-tools",
            Search = "drill",
            Page = 2,
            PageSize = 10,
        });

        var query = stub.Requests.Single().RequestUri!.Query;
        Assert.Contains("category=power-tools", query);
        Assert.Contains("search=drill", query);
        Assert.Contains("page=2", query);
        Assert.Contains("pageSize=10", query);

        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(17, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_OmitsEmptyFilters_StillIncludesPaging()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = Array.Empty<object>(),
            page = 1,
            pageSize = 20,
            totalCount = 0,
        }));
        var repo = BuildRepo(stub);

        await repo.SearchAsync(new ItemQuery());

        var query = stub.Requests.Single().RequestUri!.Query;
        Assert.DoesNotContain("category=", query);
        Assert.DoesNotContain("search=", query);
        Assert.Contains("page=1", query);
        Assert.Contains("pageSize=20", query);
    }

    // ---- GetNearbyAsync ---------------------------------------------------

    [Fact]
    public async Task GetNearbyAsync_BuildsLatLonRadiusInvariantCulture()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(Array.Empty<object>()));
        var repo = BuildRepo(stub);

        await repo.GetNearbyAsync(55.95, -3.19, 5.0, "power-tools");

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/items/nearby", uri.AbsolutePath);
        Assert.Contains("lat=55.95", uri.Query);
        Assert.Contains("lon=-3.19", uri.Query);
        Assert.Contains("radius=5", uri.Query);
        Assert.Contains("category=power-tools", uri.Query);
    }

    [Fact]
    public async Task GetNearbyAsync_PopulatesDistanceKmFromWire()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new[]
        {
            new
            {
                id = 1, title = "Drill", dailyRate = 5m,
                categoryId = 1, ownerId = 7,
                lat = 55.95, lon = -3.19,
                isAvailable = true,
                distanceKm = 0.42,
                createdAt = DateTime.UtcNow,
            },
        }));
        var repo = BuildRepo(stub);

        var result = await repo.GetNearbyAsync(55.95, -3.19, 5.0);

        Assert.Single(result);
        Assert.Equal(0.42, result[0].DistanceKm);
    }

    // ---- CreateAsync ------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PostsExpectedBody_AndParsesResponse()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 99,
            title = "New Item",
            dailyRate = 12.5m,
            categoryId = 3,
            ownerId = 7,
            lat = 1.0,
            lon = 2.0,
            isAvailable = true,
            createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        var created = await repo.CreateAsync(new Item
        {
            Title = "New Item",
            Description = "desc",
            DailyRate = 12.5m,
            CategoryId = 3,
            Latitude = 1.0,
            Longitude = 2.0,
        });

        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/items", request.RequestUri!.AbsolutePath);

        var bodyJson = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        var root = doc.RootElement;
        Assert.Equal("New Item", root.GetProperty("title").GetString());
        Assert.Equal("desc",     root.GetProperty("description").GetString());
        Assert.Equal(12.5m,      root.GetProperty("dailyRate").GetDecimal());
        Assert.Equal(3,          root.GetProperty("categoryId").GetInt32());
        Assert.Equal(1.0,        root.GetProperty("lat").GetDouble());
        Assert.Equal(2.0,        root.GetProperty("lon").GetDouble());
        // OwnerId is implicit from JWT — must NOT be in the body
        Assert.False(root.TryGetProperty("ownerId", out _));

        Assert.Equal(99, created.Id);
    }

    // ---- UpdateAsync ------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_PutsAllowedFields_OnIdRoute()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 42,
            title = "Renamed",
            dailyRate = 7m,
            categoryId = 1,
            ownerId = 7,
            lat = 0.0, lon = 0.0,
            isAvailable = false,
            createdAt = DateTime.UtcNow,
        }));
        var repo = BuildRepo(stub);

        var updated = await repo.UpdateAsync(new Item
        {
            Id = 42,
            Title = "Renamed",
            Description = "x",
            DailyRate = 7m,
            CategoryId = 1,    // not allowed in PUT — should be silently dropped
            IsAvailable = false,
        });

        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/items/42", request.RequestUri!.AbsolutePath);

        var body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("Renamed", root.GetProperty("title").GetString());
        Assert.False(root.GetProperty("isAvailable").GetBoolean());
        Assert.False(root.TryGetProperty("categoryId", out _));
        Assert.False(root.TryGetProperty("lat", out _));

        Assert.Equal("Renamed", updated.Title);
    }

    // ---- GetReviewsAsync --------------------------------------------------

    [Fact]
    public async Task GetReviewsAsync_HitsItemsIdReviewsEndpoint_WithPaging()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = new[]
            {
                new
                {
                    id = 1, rentalId = 10, reviewerId = 5,
                    reviewerName = "Bob", itemTitle = "Drill",
                    rating = 5, comment = "Great",
                    createdAt = DateTime.UtcNow,
                },
            },
            page = 1,
            pageSize = 20,
            totalCount = 1,
        }));
        var repo = BuildRepo(stub);

        var result = await repo.GetReviewsAsync(42, page: 1, pageSize: 20);

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/items/42/reviews", uri.AbsolutePath);
        Assert.Contains("page=1",     uri.Query);
        Assert.Contains("pageSize=20", uri.Query);

        Assert.Single(result.Items);
        Assert.Equal("Bob", result.Items[0].ReviewerName);
    }

    // ---- DeleteAsync ------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeleteAsync(1));
    }

    // ---- Helpers ----------------------------------------------------------

    private static ApiItemRepository BuildRepo(StubHttpMessageHandler stub)
    {
        var client = new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") };
        return new ApiItemRepository(client);
    }
}
