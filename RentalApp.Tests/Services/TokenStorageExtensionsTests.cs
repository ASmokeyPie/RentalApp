using RentalApp.Services;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Services;

public class TokenStorageExtensionsTests
{
    [Fact]
    public async Task GetValidTokenAsync_ReturnsNull_WhenNoToken()
    {
        var storage = new InMemoryTokenStorage();

        var result = await storage.GetValidTokenAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetValidTokenAsync_ReturnsToken_WhenFresh()
    {
        var token = new StoredToken("jwt", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);

        var result = await storage.GetValidTokenAsync();

        Assert.NotNull(result);
        Assert.Equal("jwt", result!.Token);
    }

    [Fact]
    public async Task GetValidTokenAsync_ReturnsNull_WhenExpired()
    {
        var token = new StoredToken("jwt", DateTime.UtcNow.AddMinutes(-5), UserId: 1);
        var storage = new InMemoryTokenStorage(token);

        var result = await storage.GetValidTokenAsync();

        Assert.Null(result);
    }
}
