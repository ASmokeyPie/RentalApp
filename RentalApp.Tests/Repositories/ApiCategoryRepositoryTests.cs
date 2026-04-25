using System.Net;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories.Api;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Repositories;

public class ApiCategoryRepositoryTests
{
    [Fact]
    public async Task ListAsync_HitsCategoriesEndpoint_AndParsesArray()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new[]
        {
            new { id = 1, name = "Power Tools",  slug = "power-tools",  itemCount = 5 },
            new { id = 2, name = "Camping Gear", slug = "camping-gear", itemCount = 3 },
        }));
        var repo = BuildRepo(stub);

        var result = await repo.ListAsync();

        var request = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/categories", request.RequestUri!.AbsolutePath);

        Assert.Equal(2, result.Count);
        Assert.Equal("Power Tools", result[0].Name);
        Assert.Equal("camping-gear", result[1].Slug);
    }

    [Fact]
    public async Task GetByIdAsync_FindsMatchingCategoryFromList()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(new[]
        {
            new { id = 1, name = "Power Tools",  slug = "power-tools",  itemCount = 5 },
            new { id = 2, name = "Camping Gear", slug = "camping-gear", itemCount = 3 },
        }));
        var repo = BuildRepo(stub);

        var category = await repo.GetByIdAsync(2);

        Assert.NotNull(category);
        Assert.Equal("Camping Gear", category!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotInList()
    {
        var stub = new StubHttpMessageHandler(TestResponses.Json(Array.Empty<object>()));
        var repo = BuildRepo(stub);

        var category = await repo.GetByIdAsync(99);

        Assert.Null(category);
    }

    [Fact]
    public async Task CreateAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => repo.CreateAsync(new Category { Name = "X", Slug = "x" }));
    }

    [Fact]
    public async Task UpdateAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => repo.UpdateAsync(new Category { Id = 1, Name = "X", Slug = "x" }));
    }

    [Fact]
    public async Task DeleteAsync_Throws_NotSupported()
    {
        var repo = BuildRepo(new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK)));

        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeleteAsync(1));
    }

    private static ApiCategoryRepository BuildRepo(StubHttpMessageHandler stub)
    {
        var client = new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") };
        return new ApiCategoryRepository(client);
    }
}
