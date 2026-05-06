using RentalApp.Services;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Services;

public class TokenStorageExtensionsTests
{
    [Fact]
    public async Task GetValidTokenAsync_ReturnsNull_WhenNoToken()
    {
        // Arrange
        var storage = new InMemoryTokenStorage();

        // Act
        var result = await storage.GetValidTokenAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetValidTokenAsync_ReturnsToken_WhenFresh()
    {
        // Arrange
        var token = new StoredToken("jwt", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);

        // Act
        var result = await storage.GetValidTokenAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("jwt", result!.Token);
    }

    [Fact]
    public async Task GetValidTokenAsync_ReturnsNull_WhenExpired()
    {
        // Arrange
        var token = new StoredToken("jwt", DateTime.UtcNow.AddMinutes(-5), UserId: 1);
        var storage = new InMemoryTokenStorage(token);

        // Act
        var result = await storage.GetValidTokenAsync();

        // Assert
        Assert.Null(result);
    }
}
