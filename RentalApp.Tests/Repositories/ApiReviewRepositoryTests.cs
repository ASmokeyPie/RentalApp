using System.Net;
using System.Text.Json;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories.Api;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

public class ApiReviewRepositoryTests
{
    // ---- CreateAsync (specialised) ----------------------------------------

    [Fact]
    public async Task CreateAsync_PostsReviewsWithExpectedBody()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 1,
            rentalId = 10,
            reviewerId = 5,
            reviewerName = "Ada",
            itemTitle = "Drill",
            rating = 5,
            comment = "Loved it",
            createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        var review = await repo.CreateAsync(rentalId: 10, rating: 5, comment: "Loved it");

        var request = stub.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/reviews", request.RequestUri!.AbsolutePath);

        var bodyJson = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        var root = doc.RootElement;
        Assert.Equal(10, root.GetProperty("rentalId").GetInt32());
        Assert.Equal(5,  root.GetProperty("rating").GetInt32());
        Assert.Equal("Loved it", root.GetProperty("comment").GetString());

        Assert.Equal(1, review.Id);
        Assert.Equal("Ada", review.ReviewerName);
        Assert.Equal("Drill", review.ItemTitle);
    }

    [Fact]
    public async Task CreateAsync_OmitsCommentWhenNull()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 1,
            rentalId = 10,
            reviewerId = 5,
            rating = 4,
            createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        await repo.CreateAsync(rentalId: 10, rating: 4, comment: null);

        var bodyJson = await stub.Requests.Single().Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        // ApiJsonOptions sets DefaultIgnoreCondition = WhenWritingNull
        Assert.False(doc.RootElement.TryGetProperty("comment", out _));
    }

    [Fact]
    public async Task CreateAsync_FromEntity_DelegatesToSpecialised()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 2, rentalId = 7, reviewerId = 3,
            rating = 3, createdAt = DateTime.UtcNow,
        }, HttpStatusCode.Created));
        var repo = BuildRepo(stub);

        var review = await repo.CreateAsync(new Review
        {
            RentalId = 7, ReviewerId = 3, Rating = 3, Comment = "ok",
        });

        var bodyJson = await stub.Requests.Single().Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        Assert.Equal(7, doc.RootElement.GetProperty("rentalId").GetInt32());
        Assert.Equal(2, review.Id);
    }

    // ---- GetForUserAsync / GetForItemAsync --------------------------------

    [Fact]
    public async Task GetForUserAsync_HitsUserReviewsEndpoint_WithPaging()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = Array.Empty<object>(),
            page = 1,
            pageSize = 20,
            totalCount = 0,
        }));
        var repo = BuildRepo(stub);

        await repo.GetForUserAsync(userId: 5);

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/users/5/reviews", uri.AbsolutePath);
        Assert.Contains("page=1",     uri.Query);
        Assert.Contains("pageSize=20", uri.Query);
    }

    [Fact]
    public async Task GetForItemAsync_HitsItemReviewsEndpoint()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new
        {
            items = new[]
            {
                new
                {
                    id = 1, rentalId = 10, reviewerId = 5,
                    rating = 5, createdAt = DateTime.UtcNow,
                },
            },
            page = 1, pageSize = 20, totalCount = 1,
        }));
        var repo = BuildRepo(stub);

        var result = await repo.GetForItemAsync(itemId: 42, page: 1, pageSize: 20);

        var uri = stub.Requests.Single().RequestUri!;
        Assert.Equal("/items/42/reviews", uri.AbsolutePath);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    // ---- Methods that don't fit the API -----------------------------------

    [Fact]
    public async Task GetByIdAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.GetByIdAsync(1));
    }

    [Fact]
    public async Task ListAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task UpdateAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.UpdateAsync(new Review()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeleteAsync(1));
    }

    // ---- Helpers ----------------------------------------------------------

    private static ApiReviewRepository BuildRepo(StubHttpMessageHandler stub)
    {
        var client = new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") };
        return new ApiReviewRepository(client);
    }
}
