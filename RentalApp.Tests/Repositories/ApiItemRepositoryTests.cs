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
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.NotFound));
        var repo = BuildRepo(stub);

        // Act
        var item = await repo.GetByIdAsync(42);

        // Assert
        Assert.Null(item);
        Assert.Equal("/items/42", stub.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_ParsesFullDetail_On200()
    {
        // Spec: GET /items/{id} returns latitude/longitude (not lat/lon),
        // plus totalReviews and an inline reviews array.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 42,
            title = "Cordless Drill",
            description = "An 18V drill",
            dailyRate = 5.50m,
            categoryId = 1,
            category = "Power Tools",
            ownerId = 7,
            ownerName = "Ada L.",
            ownerRating = 4.7,
            latitude = 55.95,
            longitude = -3.19,
            isAvailable = true,
            averageRating = 4.5,
            totalReviews = 8,
            createdAt = DateTime.UtcNow,
            reviews = Array.Empty<object>(),
        }));
        var repo = BuildRepo(stub);

        // Act
        var item = await repo.GetByIdAsync(42);

        // Assert
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
    }

    [Fact]
    public async Task GetByIdAsync_PopulatesInlineReviews_AndTotalReviews()
    {
        // Phase 7b: surface the inline reviews array + totalReviews count
        // so ItemDetailsPage can render them without an extra round-trip.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 42,
            title = "Drill",
            description = (string?)null,
            dailyRate = 5m,
            categoryId = 1,
            category = "Power Tools",
            ownerId = 7,
            ownerName = "Ada",
            ownerRating = (double?)null,
            latitude = 0.0,
            longitude = 0.0,
            isAvailable = true,
            averageRating = 4.5,
            totalReviews = 3,
            createdAt = DateTime.UtcNow,
            reviews = new[]
            {
                new
                {
                    id = 100, reviewerId = 5, reviewerName = "Bob",
                    rating = 5, comment = "Loved it",
                    createdAt = new DateTime(2026, 4, 10),
                },
                new
                {
                    id = 101, reviewerId = 6, reviewerName = "Cara",
                    rating = 4, comment = (string?)null,
                    createdAt = new DateTime(2026, 4, 12),
                },
            },
        }));
        var repo = BuildRepo(stub);

        // Act
        var item = await repo.GetByIdAsync(42);

        // Assert
        Assert.NotNull(item);
        Assert.Equal(3, item!.TotalReviews);
        Assert.Equal(2, item.Reviews.Count);
        Assert.Equal("Bob", item.Reviews[0].ReviewerName);
        Assert.Equal(5, item.Reviews[0].Rating);
        Assert.Equal("Loved it", item.Reviews[0].Comment);
        Assert.Null(item.Reviews[1].Comment);   // null comment round-trips as null
    }

    // ---- SearchAsync (paginated GET /items) -------------------------------

    [Fact]
    public async Task SearchAsync_BuildsQueryString_AndParsesPagedEnvelope()
    {
        // Spec: GET /items list response is { items, totalItems, page, pageSize, totalPages }
        // and the items in the list have NO latitude/longitude.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = new[]
            {
                new
                {
                    id = 1, title = "Drill", description = (string?)null,
                    dailyRate = 5m,
                    categoryId = 1, category = "Power Tools",
                    ownerId = 7, ownerName = "Ada",
                    ownerRating = (double?)null,
                    isAvailable = true,
                    averageRating = (double?)null,
                    createdAt = DateTime.UtcNow,
                },
            },
            totalItems = 17,
            page = 2,
            pageSize = 10,
            totalPages = 2,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.SearchAsync(new ItemQuery
        {
            CategorySlug = "power-tools",
            Search = "drill",
            Page = 2,
            PageSize = 10,
        });

        // Assert
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
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = Array.Empty<object>(),
            totalItems = 0,
            page = 1,
            pageSize = 20,
            totalPages = 0,
        }));
        var repo = BuildRepo(stub);

        // Act
        await repo.SearchAsync(new ItemQuery());

        // Assert
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
        // Spec: GET /items/nearby returns an envelope with searchLocation/radius/totalResults.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = Array.Empty<object>(),
            searchLocation = new { latitude = 55.95, longitude = -3.19 },
            radius = 5.0,
            totalResults = 0,
        }));
        var repo = BuildRepo(stub);

        // Act
        await repo.GetNearbyAsync(55.95, -3.19, 5.0, "power-tools");

        // Assert
        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/items/nearby", uri.AbsolutePath);
        // Query params use the short names per spec (lat/lon/radius/category).
        Assert.Contains("lat=55.95", uri.Query);
        Assert.Contains("lon=-3.19", uri.Query);
        Assert.Contains("radius=5", uri.Query);
        Assert.Contains("category=power-tools", uri.Query);
    }

    [Fact]
    public async Task GetNearbyAsync_PopulatesDistanceFromWire()
    {
        // Spec: nearby items use latitude/longitude and 'distance' (not distanceKm).
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = new[]
            {
                new
                {
                    id = 1, title = "Drill", description = (string?)null,
                    dailyRate = 5m,
                    categoryId = 1, category = "Power Tools",
                    ownerId = 7, ownerName = "Ada",
                    latitude = 55.95, longitude = -3.19,
                    distance = 0.42,
                    isAvailable = true,
                    averageRating = (double?)null,
                },
            },
            searchLocation = new { latitude = 55.95, longitude = -3.19 },
            radius = 5.0,
            totalResults = 1,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.GetNearbyAsync(55.95, -3.19, 5.0);

        // Assert
        Assert.Single(result);
        Assert.Equal(0.42, result[0].DistanceKm);
        Assert.Equal(55.95, result[0].Latitude);
        Assert.Equal(-3.19, result[0].Longitude);
    }

    // ---- CreateAsync ------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PostsExpectedBody_AndParsesResponse()
    {
        // Spec: POST /items takes latitude/longitude (full names) in the body
        // and ownerId is implicit from the JWT — must NOT be in the body.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 99,
            title = "New Item",
            description = "desc",
            dailyRate = 12.5m,
            categoryId = 3,
            category = "Power Tools",
            ownerId = 7,
            ownerName = "Ada",
            latitude = 1.0,
            longitude = 2.0,
            isAvailable = true,
            createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        // Act
        var created = await repo.CreateAsync(new Item
        {
            Title = "New Item",
            Description = "desc",
            DailyRate = 12.5m,
            CategoryId = 3,
            Latitude = 1.0,
            Longitude = 2.0,
        });

        // Assert
        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/items", request.RequestUri!.AbsolutePath);

        var bodyJson = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        var root = doc.RootElement;
        Assert.Equal("New Item", root.GetProperty("title").GetString());
        Assert.Equal("desc", root.GetProperty("description").GetString());
        Assert.Equal(12.5m, root.GetProperty("dailyRate").GetDecimal());
        Assert.Equal(3, root.GetProperty("categoryId").GetInt32());
        Assert.Equal(1.0, root.GetProperty("latitude").GetDouble());
        Assert.Equal(2.0, root.GetProperty("longitude").GetDouble());
        // OwnerId is implicit from JWT — must NOT be in the body
        Assert.False(root.TryGetProperty("ownerId", out _));
        // The short forms must NOT be in the body either.
        Assert.False(root.TryGetProperty("lat", out _));
        Assert.False(root.TryGetProperty("lon", out _));

        Assert.Equal(99, created.Id);
        Assert.Equal(1.0, created.Latitude);
        Assert.Equal(2.0, created.Longitude);
    }

    // ---- UpdateAsync ------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_PutsAllowedFields_OnIdRoute_AndMergesSlimResponse()
    {
        // Spec: PUT /items/{id} returns a slim 5-field response.
        // The repo merges those fields onto the entity caller passed in
        // rather than fabricating a full Item from the slim shape.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 42,
            title = "Renamed",
            description = (string?)null,
            dailyRate = 7m,
            isAvailable = false,
        }));
        var repo = BuildRepo(stub);

        // Act
        var updated = await repo.UpdateAsync(new Item
        {
            Id = 42,
            Title = "Renamed",
            Description = "x",
            DailyRate = 7m,
            CategoryId = 1,    // not allowed in PUT — should be silently dropped from body
            Latitude = 55.95,  // not allowed in PUT — should be silently dropped from body
            Longitude = -3.19,
            IsAvailable = false,
        });

        // Assert
        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/items/42", request.RequestUri!.AbsolutePath);

        var body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("Renamed", root.GetProperty("title").GetString());
        Assert.False(root.GetProperty("isAvailable").GetBoolean());
        Assert.False(root.TryGetProperty("categoryId", out _));
        Assert.False(root.TryGetProperty("latitude", out _));
        Assert.False(root.TryGetProperty("longitude", out _));

        // Merged confirmed fields from server response.
        Assert.Equal("Renamed", updated.Title);
        Assert.False(updated.IsAvailable);
        // Caller-supplied fields preserved (server doesn't echo these on PUT).
        Assert.Equal(1, updated.CategoryId);
        Assert.Equal(55.95, updated.Latitude);
    }

    // ---- GetReviewsAsync --------------------------------------------------

    [Fact]
    public async Task GetReviewsAsync_HitsItemsIdReviewsEndpoint_WithEnvelope()
    {
        // Spec: GET /items/{id}/reviews returns
        // { reviews, averageRating, totalReviews, page, pageSize, totalPages }.
        // Arrange
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            reviews = new[]
            {
                new
                {
                    id = 1, rentalId = 10, reviewerId = 5,
                    reviewerName = "Bob",
                    rating = 5, comment = "Great",
                    createdAt = DateTime.UtcNow,
                },
            },
            averageRating = 4.5,
            totalReviews = 1,
            page = 1,
            pageSize = 20,
            totalPages = 1,
        }));
        var repo = BuildRepo(stub);

        // Act
        var result = await repo.GetReviewsAsync(42, page: 1, pageSize: 20);

        // Assert
        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/items/42/reviews", uri.AbsolutePath);
        Assert.Contains("page=1", uri.Query);
        Assert.Contains("pageSize=20", uri.Query);

        Assert.Single(result.Items);
        Assert.Equal("Bob", result.Items[0].ReviewerName);
        Assert.Equal(1, result.TotalCount);
    }

    // ---- DeleteAsync ------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Throws_NotSupported()
    {
        // Arrange
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        // Act + Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeleteAsync(1));
    }

    // ---- Helpers ----------------------------------------------------------

    private static ApiItemRepository BuildRepo(StubHttpMessageHandler stub)
    {
        // Arrange: repository under test with a stubbed HttpClient.
        var client = new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") };
        return new ApiItemRepository(client);
    }
}
