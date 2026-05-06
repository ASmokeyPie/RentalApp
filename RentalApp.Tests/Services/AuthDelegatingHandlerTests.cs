using System.Net;
using RentalApp.Services;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Services;

public class AuthDelegatingHandlerTests
{
    [Fact]
    public async Task AttachesBearerToken_WhenValidTokenStored()
    {
        // Arrange: a valid, non-expired token is in storage.
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out _);

        // Act: perform any request through the handler pipeline.
        await client.GetAsync("items");

        // Assert: the outgoing request includes the expected Bearer token.
        var request = Assert.Single(inner.Requests);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("jwt-value", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task DoesNotAttachBearerToken_WhenNoTokenStored()
    {
        // Arrange: storage has no token.
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out _);

        // Act
        await client.PostAsync("auth/token", new StringContent("{}"));

        // Assert: no Authorization header is added.
        var request = Assert.Single(inner.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task DoesNotAttachBearerToken_WhenTokenExpired()
    {
        // Arrange: token is expired.
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddMinutes(-5), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out _);

        // Act
        await client.GetAsync("items");

        // Assert: expired token is not attached.
        var request = Assert.Single(inner.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task On401WithAttachedToken_ClearsStorageAndRaisesEvent()
    {
        // Arrange: token is attached and the API responds with 401.
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = BuildClient(storage, inner, out var handler);

        var fired = 0;
        handler.AuthenticationExpired += (_, _) => fired++;

        // Act
        await client.GetAsync("items");

        // Assert: handler clears the token and raises the event.
        Assert.Equal(1, fired);
        Assert.Equal(1, storage.ClearCallCount);
        Assert.Null(await storage.LoadAsync());
    }

    [Fact]
    public async Task On401WithoutAttachedToken_DoesNothing()
    {
        // e.g. a failed POST /auth/token — 401 with no token is "bad
        // credentials", not "session expired".
        // Arrange: no token exists so none can be attached.
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = BuildClient(storage, inner, out var handler);

        var fired = 0;
        handler.AuthenticationExpired += (_, _) => fired++;

        // Act
        await client.PostAsync("auth/token", new StringContent("{}"));

        // Assert: no event and storage remains unchanged.
        Assert.Equal(0, fired);
        Assert.Equal(0, storage.ClearCallCount);
    }

    [Fact]
    public async Task On200_DoesNotFireEvent()
    {
        // Arrange: token is attached but response is 200.
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out var handler);

        var fired = 0;
        handler.AuthenticationExpired += (_, _) => fired++;

        // Act
        await client.GetAsync("items");

        // Assert: successful responses do not trigger expiry behavior.
        Assert.Equal(0, fired);
        Assert.Equal(0, storage.ClearCallCount);
    }

    private static HttpClient BuildClient(
        InMemoryTokenStorage storage,
        StubHttpMessageHandler inner,
        out AuthDelegatingHandler handler)
    {
        // Arrange: build a client whose handler chain includes AuthDelegatingHandler.
        handler = new AuthDelegatingHandler(storage) { InnerHandler = inner };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        };
    }
}
