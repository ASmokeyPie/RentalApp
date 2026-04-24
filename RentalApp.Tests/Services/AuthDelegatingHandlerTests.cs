using System.Net;
using RentalApp.Services;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Services;

public class AuthDelegatingHandlerTests
{
    [Fact]
    public async Task AttachesBearerToken_WhenValidTokenStored()
    {
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out _);

        await client.GetAsync("items");

        var request = Assert.Single(inner.Requests);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("jwt-value", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task DoesNotAttachBearerToken_WhenNoTokenStored()
    {
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out _);

        await client.PostAsync("auth/token", new StringContent("{}"));

        var request = Assert.Single(inner.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task DoesNotAttachBearerToken_WhenTokenExpired()
    {
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddMinutes(-5), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out _);

        await client.GetAsync("items");

        var request = Assert.Single(inner.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task On401WithAttachedToken_ClearsStorageAndRaisesEvent()
    {
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = BuildClient(storage, inner, out var handler);

        var fired = 0;
        handler.AuthenticationExpired += (_, _) => fired++;

        await client.GetAsync("items");

        Assert.Equal(1, fired);
        Assert.Equal(1, storage.ClearCallCount);
        Assert.Null(await storage.LoadAsync());
    }

    [Fact]
    public async Task On401WithoutAttachedToken_DoesNothing()
    {
        // e.g. a failed POST /auth/token — 401 with no token is "bad
        // credentials", not "session expired".
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = BuildClient(storage, inner, out var handler);

        var fired = 0;
        handler.AuthenticationExpired += (_, _) => fired++;

        await client.PostAsync("auth/token", new StringContent("{}"));

        Assert.Equal(0, fired);
        Assert.Equal(0, storage.ClearCallCount);
    }

    [Fact]
    public async Task On200_DoesNotFireEvent()
    {
        var token = new StoredToken("jwt-value", DateTime.UtcNow.AddHours(1), UserId: 1);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = BuildClient(storage, inner, out var handler);

        var fired = 0;
        handler.AuthenticationExpired += (_, _) => fired++;

        await client.GetAsync("items");

        Assert.Equal(0, fired);
        Assert.Equal(0, storage.ClearCallCount);
    }

    private static HttpClient BuildClient(
        InMemoryTokenStorage storage,
        StubHttpMessageHandler inner,
        out AuthDelegatingHandler handler)
    {
        handler = new AuthDelegatingHandler(storage) { InnerHandler = inner };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        };
    }
}
