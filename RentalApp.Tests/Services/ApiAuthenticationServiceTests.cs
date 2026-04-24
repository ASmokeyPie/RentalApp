using System.Net;
using RentalApp.Services;
using RentalApp.Tests.Support;

namespace RentalApp.Tests.Services;

public class ApiAuthenticationServiceTests
{
    // ---- Login ----

    [Fact]
    public async Task LoginAsync_OnSuccess_StoresTokenAndSetsCurrentUser()
    {
        var storage = new InMemoryTokenStorage();
        var inner = StubHttpMessageHandler.Sequence(
            TestResponses.Json(new
            {
                token = "jwt-value",
                expiresAt = DateTime.UtcNow.AddHours(1),
                userId = 7,
            }),
            TestResponses.Json(new
            {
                id = 7,
                email = "a@b.com",
                firstName = "Ada",
                lastName = "Lovelace",
                createdAt = DateTime.UtcNow,
            }));
        var (service, _, _) = BuildService(storage, inner);

        var events = new List<bool>();
        service.AuthenticationStateChanged += (_, isAuth) => events.Add(isAuth);

        var result = await service.LoginAsync("a@b.com", "pw");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, storage.SaveCallCount);
        var stored = await storage.LoadAsync();
        Assert.NotNull(stored);
        Assert.Equal("jwt-value", stored!.Token);
        Assert.NotNull(service.CurrentUser);
        Assert.Equal(7, service.CurrentUser!.Id);
        Assert.True(service.IsAuthenticated);
        Assert.Equal(new[] { true }, events);
    }

    [Fact]
    public async Task LoginAsync_On401_DoesNotStoreTokenOrRaiseEvent()
    {
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(
            TestResponses.Json(new { error = "Unauthorized", message = "Invalid email or password" },
                HttpStatusCode.Unauthorized));
        var (service, _, _) = BuildService(storage, inner);

        var events = new List<bool>();
        service.AuthenticationStateChanged += (_, isAuth) => events.Add(isAuth);

        var result = await service.LoginAsync("a@b.com", "wrong");

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid email or password", result.Message);
        Assert.Equal(0, storage.SaveCallCount);
        Assert.Null(service.CurrentUser);
        Assert.False(service.IsAuthenticated);
        Assert.Empty(events);
    }

    [Fact]
    public async Task LoginAsync_WhenMeCallFails_ClearsToken()
    {
        var storage = new InMemoryTokenStorage();
        var inner = StubHttpMessageHandler.Sequence(
            TestResponses.Json(new
            {
                token = "jwt",
                expiresAt = DateTime.UtcNow.AddHours(1),
                userId = 7,
            }),
            TestResponses.Status(HttpStatusCode.InternalServerError));
        var (service, _, _) = BuildService(storage, inner);

        var result = await service.LoginAsync("a@b.com", "pw");

        Assert.False(result.IsSuccess);
        Assert.Null(await storage.LoadAsync());
        Assert.Null(service.CurrentUser);
    }

    // ---- Logout ----

    [Fact]
    public async Task LogoutAsync_ClearsTokenAndCurrentUserAndRaisesEvent()
    {
        var token = new StoredToken("jwt", DateTime.UtcNow.AddHours(1), UserId: 7);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7, email = "a@b.com", firstName = "A", lastName = "B", createdAt = DateTime.UtcNow,
        }));
        var (service, _, _) = BuildService(storage, inner);
        Assert.True(await service.TryRestoreSessionAsync());

        var events = new List<bool>();
        service.AuthenticationStateChanged += (_, isAuth) => events.Add(isAuth);

        await service.LogoutAsync();

        Assert.Null(await storage.LoadAsync());
        Assert.Null(service.CurrentUser);
        Assert.Equal(new[] { false }, events);
    }

    // ---- Session restore ----

    [Fact]
    public async Task TryRestoreSessionAsync_ReturnsFalse_WhenNoToken()
    {
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK));
        var (service, _, _) = BuildService(storage, inner);

        var restored = await service.TryRestoreSessionAsync();

        Assert.False(restored);
        Assert.Null(service.CurrentUser);
        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_ReturnsFalse_WhenTokenExpired()
    {
        var token = new StoredToken("jwt", DateTime.UtcNow.AddMinutes(-1), UserId: 7);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(TestResponses.Status(HttpStatusCode.OK));
        var (service, _, _) = BuildService(storage, inner);

        var restored = await service.TryRestoreSessionAsync();

        Assert.False(restored);
        Assert.Null(service.CurrentUser);
        Assert.Empty(inner.Requests); // Expired token short-circuits before hitting /users/me
    }

    [Fact]
    public async Task TryRestoreSessionAsync_RehydratesCurrentUser_WhenTokenValid()
    {
        var token = new StoredToken("jwt", DateTime.UtcNow.AddHours(1), UserId: 7);
        var storage = new InMemoryTokenStorage(token);
        var inner = new StubHttpMessageHandler(TestResponses.Json(new
        {
            id = 7,
            email = "a@b.com",
            firstName = "Ada",
            lastName = "Lovelace",
            createdAt = DateTime.UtcNow,
        }));
        var (service, _, _) = BuildService(storage, inner);

        var events = new List<bool>();
        service.AuthenticationStateChanged += (_, isAuth) => events.Add(isAuth);

        var restored = await service.TryRestoreSessionAsync();

        Assert.True(restored);
        Assert.NotNull(service.CurrentUser);
        Assert.Equal(7, service.CurrentUser!.Id);
        Assert.Equal("Ada", service.CurrentUser.FirstName);
        Assert.Equal(new[] { true }, events);

        // And the call went out with the Bearer attached:
        var request = Assert.Single(inner.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("jwt", request.Headers.Authorization?.Parameter);
    }

    // ---- 401 during an authenticated call ----

    [Fact]
    public async Task AuthenticatedCallReturning401_DropsCurrentUserAndFiresEvent()
    {
        var token = new StoredToken("jwt", DateTime.UtcNow.AddHours(1), UserId: 7);
        var storage = new InMemoryTokenStorage(token);
        var inner = StubHttpMessageHandler.Sequence(
            // First call: TryRestoreSession -> /users/me succeeds
            TestResponses.Json(new
            {
                id = 7, email = "a@b.com", firstName = "A", lastName = "B", createdAt = DateTime.UtcNow,
            }),
            // Second call: some downstream authenticated call that the app makes later
            TestResponses.Status(HttpStatusCode.Unauthorized));
        var (service, client, _) = BuildService(storage, inner);

        Assert.True(await service.TryRestoreSessionAsync());
        Assert.NotNull(service.CurrentUser);

        var events = new List<bool>();
        service.AuthenticationStateChanged += (_, isAuth) => events.Add(isAuth);

        // Simulate some other part of the app calling an authenticated endpoint:
        await client.GetAsync("items");

        Assert.Null(service.CurrentUser);
        Assert.False(service.IsAuthenticated);
        Assert.Equal(new[] { false }, events);
        Assert.Null(await storage.LoadAsync());
    }

    // ---- Registration ----

    [Fact]
    public async Task RegisterAsync_OnSuccess_ReturnsSuccessAndDoesNotLogIn()
    {
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(
            TestResponses.Json(new
            {
                id = 9, email = "c@d.com", firstName = "C", lastName = "D",
                createdAt = DateTime.UtcNow,
            }, HttpStatusCode.Created));
        var (service, _, _) = BuildService(storage, inner);

        var result = await service.RegisterAsync("C", "D", "c@d.com", "Password1");

        Assert.True(result.IsSuccess);
        Assert.Null(service.CurrentUser);
        Assert.Equal(0, storage.SaveCallCount);
    }

    [Fact]
    public async Task RegisterAsync_On400_SurfacesErrorMessage()
    {
        var storage = new InMemoryTokenStorage();
        var inner = new StubHttpMessageHandler(
            TestResponses.Json(new { error = "Validation", message = "Email already in use" },
                HttpStatusCode.BadRequest));
        var (service, _, _) = BuildService(storage, inner);

        var result = await service.RegisterAsync("C", "D", "c@d.com", "short");

        Assert.False(result.IsSuccess);
        Assert.Equal("Email already in use", result.Message);
    }

    // ---- Fixture helpers ----

    private static (ApiAuthenticationService Service, HttpClient Client, AuthDelegatingHandler Handler)
        BuildService(InMemoryTokenStorage storage, StubHttpMessageHandler inner)
    {
        var handler = new AuthDelegatingHandler(storage) { InnerHandler = inner };
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        };
        var service = new ApiAuthenticationService(client, storage, handler);
        return (service, client, handler);
    }
}
