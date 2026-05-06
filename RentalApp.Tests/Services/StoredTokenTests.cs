using RentalApp.Services;

namespace RentalApp.Tests.Services;

public class StoredTokenTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsExpired_TrueWhenExpiresAtIsInThePast()
    {
        // Arrange
        var token = new StoredToken("jwt", Now.AddMinutes(-1), UserId: 1);

        // Act + Assert
        Assert.True(token.IsExpired(now: Now, skew: TimeSpan.Zero));
    }

    [Fact]
    public void IsExpired_FalseWhenWellInTheFuture()
    {
        // Arrange
        var token = new StoredToken("jwt", Now.AddHours(1), UserId: 1);

        // Act + Assert
        Assert.False(token.IsExpired(now: Now, skew: TimeSpan.Zero));
    }

    [Fact]
    public void IsExpired_TrueWhenWithinSkewWindow()
    {
        // Token still technically valid for 20 seconds, but our default 30s
        // skew means we treat it as expired to avoid sending a request that
        // dies in flight.
        // Arrange
        var token = new StoredToken("jwt", Now.AddSeconds(20), UserId: 1);

        // Act + Assert
        Assert.True(token.IsExpired(now: Now));
    }

    [Fact]
    public void IsExpired_FalseWhenJustOutsideSkewWindow()
    {
        // Arrange
        var token = new StoredToken("jwt", Now.AddSeconds(60), UserId: 1);

        // Act + Assert
        Assert.False(token.IsExpired(now: Now));
    }
}
