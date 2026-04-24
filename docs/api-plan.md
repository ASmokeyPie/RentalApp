# RentalApp — API Integration & Coursework Plan (MAUI Client)

Last updated: 2026-04-21

**Scope.** Build the .NET MAUI Android client against the hosted SET09102
"Library of Things" API at `https://set09102-api.b-davison.workers.dev/`.
We are not building the server. Deploy locally to an Android emulator via
`adb`. Deliverables must satisfy the coursework requirements in §0 below.

---

## 0. Coursework requirements → implementation map

Every requirement below resolves to a concrete artefact in the codebase.
This table is the contract; everything that follows elaborates on it.

### Pass requirements

| # | Requirement                              | Where it lives in this codebase                                                                                                                   |
|---|------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 | User authentication, JWT storage         | `Services/Auth/ApiAuthenticationService.cs` (token), `Services/Auth/SecureStorageTokenStore.cs` (persist), `Services/Http/AuthHeaderHandler.cs` (attach on every request, handle 401 + expiry) |
| 2 | Item management (create/list/view/update)| `Repositories/IItemRepository.cs` + `HttpItemRepository.cs`; `BrowseItemsPage`, `ItemDetailPage`, `CreateItemPage`, `EditItemPage`                 |
| 3 | Basic rental request (create + list)     | `Repositories/IRentalRepository.cs`; `CreateRentalPage`, `MyRentalsPage` with incoming/outgoing tabs                                                |
| 4 | MVVM architecture                        | Existing `BaseViewModel` pattern with `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). One ViewModel per page. No code-behind logic. |
| 5 | Repository Pattern with `IRepository<T>` | `Repositories/IRepository.cs` (generic CRUD contract) + per-resource repos. ViewModels never see `HttpClient`.                                     |

### Merit / distinction requirements

| # | Requirement                          | Where it lives                                                                                                                                                  |
|---|--------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 6 | Location-based discovery (PostGIS)   | Server already implements `ST_DWithin` / `GEOGRAPHY(POINT, 4326)` behind `GET /items/nearby`. Client side: `Services/LocationService.cs` wraps `Geolocation.Default` + the nearby repo call. `NearbyItemsPage`. |
| 7 | Rental workflow management           | `Services/RentalService.cs` — state machine, price calculation, double-booking prevention (client-side pre-check + server 409 handling).                         |
| 8 | Reviews & feedback                    | `Repositories/IReviewRepository.cs`, `Services/ReviewService.cs`, `WriteReviewPage`, review lists on item & profile pages.                                       |
| 9 | Service layer                         | `Services/LocationService.cs`, `RentalService.cs`, `ReviewService.cs`. Business rules live here; ViewModels call services, not repositories directly (except for simple reads). |
|10 | Comprehensive testing (>60% coverage) | `RentalApp.Tests` xUnit project. See §7.                                                                                                                         |

### Note on PostGIS requirement

The bullets under "Location-based discovery" describe server-side SQL
(`ST_DWithin`, `ST_MakePoint`, `GEOGRAPHY(POINT, 4326)`). The hosted API
already implements these behind `GET /items/nearby`. **The client does not
run PostGIS queries** — it provides coordinates and a radius and consumes
the response. The `LocationService` is where the coursework wants the
spatial concerns abstracted on the client side: GPS acquisition, unit
conversion (km ↔ metres), and delegation to the nearby repository. If the
rubric expects the student to also demonstrate the SQL, keep the PostGIS
snippets in a `docs/postgis-notes.md` as a design artefact — say so in the
report and reference the server endpoint.

---

## 1. The API in one page

JWT bearer auth. Non-auth endpoints requiring a user send
`Authorization: Bearer <token>`. All errors are `{ error, message }`.

| Resource   | Method | Path                         | Auth | Notes                                                    |
| ---------- | ------ | ---------------------------- | ---- | -------------------------------------------------------- |
| Auth       | POST   | `/auth/register`             | —    | Password ≥ 8 chars **and at least one uppercase**        |
| Auth       | POST   | `/auth/token`                | —    | Returns `{ token, expiresAt, userId }`                   |
| Users      | GET    | `/users/me`                  | ✅   | Adds `averageRating, itemsListed, rentalsCompleted`      |
| Users      | GET    | `/users/{id}/profile`        | —    | Public profile + embedded reviews array                  |
| Users      | GET    | `/users/{id}/reviews`        | —    | Paginated                                                |
| Categories | GET    | `/categories`                | —    | `{ id, name, slug, itemCount }[]`                        |
| Items      | GET    | `/items`                     | —    | Filters: `category`, `search`, `page`, `pageSize`        |
| Items      | GET    | `/items/nearby`              | —    | `lat`, `lon`, `radius` (km, ≤ 50), `category`            |
| Items      | GET    | `/items/{id}`                | —    | Full detail + reviews                                    |
| Items      | POST   | `/items`                     | ✅   | `title, description?, dailyRate, categoryId, lat, lon`   |
| Items      | PUT    | `/items/{id}`                | ✅   | Owner only. Partial: title/desc/dailyRate/isAvailable    |
| Items      | GET    | `/items/{id}/reviews`        | —    | Paginated                                                |
| Rentals    | POST   | `/rentals`                   | ✅   | `itemId, startDate, endDate` (YYYY-MM-DD). 409 on overlap|
| Rentals    | GET    | `/rentals/incoming`          | ✅   | As owner. Filter by `status`                             |
| Rentals    | GET    | `/rentals/outgoing`          | ✅   | As borrower                                              |
| Rentals    | GET    | `/rentals/{id}`              | ✅   | Owner or borrower only                                   |
| Rentals    | PATCH  | `/rentals/{id}/status`       | ✅   | State transition. 409 on invalid                         |
| Reviews    | POST   | `/reviews`                   | ✅   | `rentalId, rating (1–5), comment?`. 409 if already done  |

### Rental state machine (per coursework)

```
Requested ─┬─▶ Approved ─▶ OutForRent ─▶ Returned ─▶ Completed ─▶ (Reviewed)
           └─▶ Rejected
Requested ─▶ Cancelled   (borrower only, while Requested)
```

Exact API status strings must be **verified against the live API** once
(call PATCH with each candidate and note which are accepted). Encode the
allowed-transitions graph in `RentalService` so the UI hides buttons that
would 409.

---

## 2. Current client state & required fixes

### Already wired

- `MauiProgram.cs` points at the hosted API and registers
  `ApiAuthenticationService` as `IAuthenticationService`.
- `/auth/register`, `/auth/token`, `/users/me` calls exist.
- Pages/VMs for Login, Register, Main, UserList, UserDetail, About, Temp.
- `CommunityToolkit.Mvvm` is present (`[ObservableProperty]`, `[RelayCommand]`,
  `ObservableObject`) — MVVM requirement #4 is already half-satisfied.

### Concrete fixes before anything new

1. **`UserProfileResponse` missing three fields.** Add `averageRating`,
   `itemsListed`, `rentalsCompleted`. Surface them on the home screen.
2. **Password rule mismatch.** `RegisterViewModel.ValidateForm` checks length
   ≥ 6. API requires ≥ 8 **and** an uppercase letter. Update the validator
   and show the rules next to the input.
3. **Token not persisted.** Currently lives only on the singleton
   `HttpClient.DefaultRequestHeaders`. Persist via `SecureStorage`.
4. **`AddSingleton<HttpClient>` is the wrong shape** for composing handlers.
   Move to `IHttpClientFactory` with a named client.
5. **No global 401 handling.** Add an `AuthHeaderHandler` that attaches the
   token and on 401 clears the token + fires an event the shell listens for
   to redirect to Login.
6. **`UserListPage` / `UserListViewModel` have no backing endpoint.** There
   is no `GET /users`. Retire or repurpose as the new `BrowseItemsPage`.
7. **HttpClient as ViewModel dependency.** ViewModels currently depend on
   `IAuthenticationService` directly (fine for auth) but will need to stop
   calling HTTP via anything other than repositories. Enforce the rule:
   **no `HttpClient` reference outside `Repositories/` or `Services/Auth/`.**

---

## 3. Target architecture — four clear layers

```
View (XAML)
   │
   ▼
ViewModel (ObservableObject + RelayCommand)
   │              ↓ (simple reads only)
   ▼
Service (business logic, validation, orchestration)
   │
   ▼
Repository (IRepository<T> — data access abstraction)
   │
   ▼
HttpClient (via IHttpClientFactory + AuthHeaderHandler)
```

Rules:

- **Views** hold no logic. Commands and bindings only.
- **ViewModels** depend on services for anything non-trivial. Simple reads
  (e.g. `IItemRepository.ListAsync`) can be called directly from the VM.
- **Services** orchestrate multiple repositories + enforce business rules
  (state transitions, price calc, double-booking). No `HttpClient` here.
- **Repositories** are the only thing that touches `HttpClient`. Each one
  implements `IRepository<T>` + its own specialised methods.

### Folder layout

```
RentalApp/
├── Models/
│   ├── Api/                               NEW — request/response DTOs (records)
│   │   ├── AuthDtos.cs
│   │   ├── UserDtos.cs
│   │   ├── CategoryDtos.cs
│   │   ├── ItemDtos.cs
│   │   ├── RentalDtos.cs
│   │   └── ReviewDtos.cs
│   └── Domain/                            NEW — client-side view models / enums
│       ├── RentalStatus.cs                (enum matching API strings)
│       └── PagedResult.cs
├── Repositories/                          NEW
│   ├── IRepository.cs                     (generic CRUD contract)
│   ├── IItemRepository.cs / HttpItemRepository.cs
│   ├── IRentalRepository.cs / HttpRentalRepository.cs
│   ├── IReviewRepository.cs / HttpReviewRepository.cs
│   ├── ICategoryRepository.cs / HttpCategoryRepository.cs
│   └── IUserRepository.cs / HttpUserRepository.cs
├── Services/
│   ├── Auth/
│   │   ├── IAuthenticationService.cs      (existing, extend)
│   │   ├── ApiAuthenticationService.cs    (existing, rewrite on factory)
│   │   ├── ITokenStore.cs                 NEW
│   │   └── SecureStorageTokenStore.cs     NEW
│   ├── Http/
│   │   ├── AuthHeaderHandler.cs           NEW — DelegatingHandler
│   │   ├── ApiException.cs                NEW
│   │   └── HttpResponseExtensions.cs      NEW — uniform error parsing
│   ├── ILocationService.cs / LocationService.cs         NEW
│   ├── IRentalService.cs   / RentalService.cs           NEW
│   ├── IReviewService.cs   / ReviewService.cs           NEW
│   └── Navigation/ (existing)
├── ViewModels/ (one per page, add as needed)
└── Views/
```

### `IRepository<T>` generic contract

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task<T> CreateAsync(T entity, CancellationToken ct = default);
    Task<T> UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

Each specialised repo extends it and adds endpoint-specific methods:

```csharp
public interface IItemRepository : IRepository<Item>
{
    Task<PagedResult<Item>> SearchAsync(string? category, string? search,
                                        int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<NearbyItem>> GetNearbyAsync(double lat, double lon,
                                                   double radiusKm, string? category,
                                                   CancellationToken ct = default);
    Task<PagedResult<Review>> GetReviewsAsync(int itemId, int page, int pageSize,
                                              CancellationToken ct = default);
}

public interface IRentalRepository
{
    Task<Rental> CreateAsync(int itemId, DateOnly startDate, DateOnly endDate,
                             CancellationToken ct = default);
    Task<IReadOnlyList<Rental>> GetIncomingAsync(string? status = null,
                                                 CancellationToken ct = default);
    Task<IReadOnlyList<Rental>> GetOutgoingAsync(string? status = null,
                                                 CancellationToken ct = default);
    Task<Rental?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Rental> UpdateStatusAsync(int id, RentalStatus status,
                                   CancellationToken ct = default);
}

public interface IReviewRepository
{
    Task<Review> CreateAsync(int rentalId, int rating, string? comment,
                             CancellationToken ct = default);
    Task<PagedResult<Review>> GetForItemAsync(int itemId, int page, int pageSize,
                                              CancellationToken ct = default);
    Task<PagedResult<Review>> GetForUserAsync(int userId, int page, int pageSize,
                                              CancellationToken ct = default);
}
```

Note that not every resource needs all CRUD operations — e.g. the API has
no DELETE rental. `IRepository<T>` stays present for the grading rubric;
where the API lacks an operation, throw `NotSupportedException` and
document it. Better: keep the generic contract **narrow** (just `GetById`
and `List`) and put mutation on the specialised interface so
`NotSupportedException` is never needed. Pick one approach and apply it
consistently.

### Services — what lives where

**`ILocationService`** (requirement #6 & #9):

```csharp
public interface ILocationService
{
    Task<(double lat, double lon)?> TryGetCurrentLocationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NearbyItem>> FindNearbyAsync(double radiusKm,
                                                    string? category = null,
                                                    CancellationToken ct = default);
}
```

Wraps `Geolocation.Default.GetLocationAsync()` and calls
`IItemRepository.GetNearbyAsync`. Permissions (`ACCESS_FINE_LOCATION`)
handled with `Permissions.RequestAsync<Permissions.LocationWhenInUse>()`.

**`IRentalService`** (requirements #7 & #9):

```csharp
public interface IRentalService
{
    decimal CalculateTotal(decimal dailyRate, DateOnly start, DateOnly end);
    bool CanTransition(RentalStatus from, RentalStatus to);
    IReadOnlyList<RentalStatus> AllowedTransitions(RentalStatus from);

    Task<Rental> RequestAsync(int itemId, DateOnly start, DateOnly end,
                              CancellationToken ct = default);
    Task<Rental> ApproveAsync(int rentalId, CancellationToken ct = default);
    Task<Rental> RejectAsync(int rentalId, CancellationToken ct = default);
    Task<Rental> MarkOutForRentAsync(int rentalId, CancellationToken ct = default);
    Task<Rental> MarkReturnedAsync(int rentalId, CancellationToken ct = default);
    Task<Rental> CompleteAsync(int rentalId, CancellationToken ct = default);
    Task<Rental> CancelAsync(int rentalId, CancellationToken ct = default);
}
```

Implementation responsibilities:

- **State machine.** `AllowedTransitions` returns the valid next states; the
  individual action methods call `IRentalRepository.UpdateStatusAsync` only
  after verifying the transition.
- **Double-booking prevention.** Before `POST /rentals`, call a client-side
  availability check: fetch the borrower's own outgoing rentals for this
  item (or all incoming ones if they're logged in as owner) and refuse if
  dates overlap. The server also enforces this and returns 409 — service
  catches that and rethrows as a friendlier `BookingConflictException`.
- **Price calculation.** `CalculateTotal = dailyRate × (end - start + 1)`
  (inclusive of both days) — confirm the server's calculation by creating a
  test rental once and comparing. Fix the formula to match.

**`IReviewService`** (requirements #8 & #9):

```csharp
public interface IReviewService
{
    Task<Review> SubmitAsync(int rentalId, int rating, string? comment,
                             CancellationToken ct = default);
    Task<ReviewSummary> GetItemSummaryAsync(int itemId,
                                            CancellationToken ct = default);
}
```

Validates: 1 ≤ rating ≤ 5, comment length ≤ 500, rental must be in
`Completed` state, no duplicate review (server also enforces with 409).

---

## 4. Pages & ViewModels

| Page                      | VM                        | Deps (services / repos)                                       | Req # |
|---------------------------|---------------------------|---------------------------------------------------------------|-------|
| LoginPage (existing)      | LoginViewModel            | IAuthenticationService                                        | 1, 4  |
| RegisterPage (existing)   | RegisterViewModel         | IAuthenticationService                                        | 1, 4  |
| HomePage (new, replace Main) | HomeViewModel          | IAuthenticationService, IUserRepository                       | 1, 4  |
| **BrowseItemsPage**       | BrowseItemsViewModel      | ICategoryRepository, IItemRepository                          | 2, 4  |
| **ItemDetailPage**        | ItemDetailViewModel       | IItemRepository, IReviewRepository, IAuthenticationService    | 2, 4  |
| **CreateItemPage**        | CreateItemViewModel       | ICategoryRepository, IItemRepository, ILocationService        | 2, 4  |
| **EditItemPage**          | EditItemViewModel         | IItemRepository                                               | 2, 4  |
| MyListingsPage            | MyListingsViewModel       | IItemRepository, IAuthenticationService                       | 2, 4  |
| **CreateRentalPage**      | CreateRentalViewModel     | IRentalService, IItemRepository                               | 3, 7  |
| **MyRentalsPage**         | MyRentalsViewModel        | IRentalRepository                                             | 3, 4  |
| **RentalDetailPage**      | RentalDetailViewModel     | IRentalService, IRentalRepository                             | 3, 7  |
| **WriteReviewPage**       | WriteReviewViewModel      | IReviewService                                                | 8     |
| **PublicProfilePage**     | PublicProfileViewModel    | IUserRepository, IReviewRepository                            | 8     |
| **NearbyItemsPage**       | NearbyItemsViewModel      | ILocationService                                              | 6, 9  |
| UserListPage (existing)   | —                         | Retire or rename → BrowseItemsPage                            | —     |

All ViewModels derive from `BaseViewModel`, use `[ObservableProperty]` for
bindable state and `[RelayCommand]` instead of event handlers. No
code-behind logic beyond `InitializeComponent`.

---

## 5. Client-side auth plumbing (requirement 1 in detail)

### Token storage

```csharp
public interface ITokenStore
{
    Task<string?> GetTokenAsync();
    Task<DateTimeOffset?> GetExpiryAsync();
    Task SetAsync(string token, DateTimeOffset expiresAt);
    Task ClearAsync();
    event EventHandler? TokenCleared;
}
```

`SecureStorageTokenStore` uses `SecureStorage.Default` with keys
`"auth.token"` and `"auth.expiry"`. Fires `TokenCleared` on `ClearAsync`.

### `AuthHeaderHandler` (DelegatingHandler)

1. On `SendAsync`, read token via `ITokenStore.GetTokenAsync()`. If present
   and not expired, attach `Authorization: Bearer <token>`.
2. If response is **401**, call `ITokenStore.ClearAsync()`. The store fires
   `TokenCleared`; `AppShell` listens and navigates to `//login`.
3. If token expiry is < 5 minutes away, log and treat as expired (so the
   user doesn't start a long flow that dies mid-way).

### DI wiring in `MauiProgram.cs`

```csharp
builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services
    .AddHttpClient("rentalapp", c =>
    {
        c.BaseAddress = new Uri("https://set09102-api.b-davison.workers.dev/");
        c.DefaultRequestHeaders.Accept.Add(new("application/json"));
    })
    .AddHttpMessageHandler<AuthHeaderHandler>();

// Repositories & services — all singletons (stateless except for factory)
builder.Services.AddSingleton<ICategoryRepository, HttpCategoryRepository>();
builder.Services.AddSingleton<IItemRepository,     HttpItemRepository>();
builder.Services.AddSingleton<IRentalRepository,   HttpRentalRepository>();
builder.Services.AddSingleton<IReviewRepository,   HttpReviewRepository>();
builder.Services.AddSingleton<IUserRepository,     HttpUserRepository>();

builder.Services.AddSingleton<IAuthenticationService, ApiAuthenticationService>();
builder.Services.AddSingleton<ILocationService,      LocationService>();
builder.Services.AddSingleton<IRentalService,        RentalService>();
builder.Services.AddSingleton<IReviewService,        ReviewService>();
```

### Refresh strategy

This API has no explicit refresh endpoint in the OpenAPI spec, so "refresh"
here means: detect expiry (via `expiresAt` stored at login), force a new
login if we're within 5 minutes of expiry, surface a non-blocking "Session
expired — sign in again" flow. If the spec later adds `/auth/refresh`,
drop in a new handler method without touching the repositories.

---

## 6. Build slices — order of implementation

| # | Slice                                                                             | Req # covered |
|---|-----------------------------------------------------------------------------------|---------------|
| 1 | Foundations: `ITokenStore`, `AuthHeaderHandler`, `IHttpClientFactory`, `ApiException`. Rewire auth service. | 1, 4          |
| 2 | Fix register validation, persist token, auto-login-on-launch, replace MainPage with HomePage showing `/users/me` stats. | 1              |
| 3 | `IRepository<T>`, `ICategoryRepository`, `IItemRepository` read paths. `BrowseItemsPage` + `ItemDetailPage` (with reviews). | 2, 4, 5        |
| 4 | `IRentalService` (price calc + state machine) + `IRentalRepository`. `CreateRentalPage`. | 3, 4, 5, 7     |
| 5 | `MyRentalsPage` (incoming/outgoing) + `RentalDetailPage` with state-appropriate action buttons. | 3, 7           |
| 6 | `CreateItemPage` + `EditItemPage` + `MyListingsPage` (owner updates). Includes picking location via `ILocationService`. | 2, 6, 9        |
| 7 | `NearbyItemsPage` via `ILocationService` (first real use of the geo feature standalone). | 6, 9           |
| 8 | Reviews: `IReviewService` + `IReviewRepository`, `WriteReviewPage`, review display on item + user pages. | 8, 9           |
| 9 | `PublicProfilePage` + retire/replace `UserListPage`.                              | 2, 8           |
|10 | Test project: unit tests, `HttpMessageHandler` fixture, coverage to ≥60%.         | 10             |
|11 | Polish: error toasts, loading states, empty states, a11y basics.                  | —             |

---

## 7. Testing plan (requirement #10)

### Project

- `RentalApp.Tests/RentalApp.Tests.csproj`, `net10.0`, references
  `RentalApp` and `RentalApp.Database` (for the DatabaseFixture — see note).
- Packages: `xunit`, `Moq`, `FluentAssertions`, `coverlet.collector`,
  `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.TestHost` (optional for
  future API tests), `Microsoft.EntityFrameworkCore.InMemory` (for the
  DatabaseFixture).

### What to test at each layer (AAA pattern)

| Layer       | Test type      | What to assert                                                                                 |
|-------------|----------------|------------------------------------------------------------------------------------------------|
| Repositories| Unit           | Correct URL, HTTP method, serialised body, headers; correct parsing of 2xx and of error JSON.  |
| Services    | Unit           | Business rules: state machine transitions, price math, double-booking, review validation.      |
| ViewModels  | Unit           | Observable props update; commands call right service method; error state on exceptions.         |
| Auth        | Unit           | Token attached to request; 401 clears token + fires event; expiry → treated as unauthenticated. |
| Integration | Integration    | End-to-end through HttpMessageHandler fixture; auth → create item → create rental → review.     |

### Test doubles

- **`MockHttpMessageHandler`** fixture — handle requests by URL pattern and
  return canned responses (status + JSON). Used to unit-test repositories.
- **`Mock<IItemRepository>`**, **`Mock<IRentalRepository>`**,
  **`Mock<ILocationService>`** (explicit coursework requirement), etc. —
  used when testing services and ViewModels.
- **`InMemoryTokenStore`** — lightweight `ITokenStore` for tests, no
  `SecureStorage`.

### The `DatabaseFixture` question

The coursework requires a `DatabaseFixture` for integration tests. In a
client that only talks to an HTTP API, there's no database to fixture.
Two honest options:

1. **Keep the existing `RentalApp.Database` project in play** for tests.
   Write a small set of integration tests that exercise the EF models +
   migrations against an in-memory or SQLite provider inside a
   `DatabaseFixture : IAsyncLifetime`. This satisfies the letter of the
   rubric but the code under test isn't on the hot path.
2. **Rename semantically to `ApiFixture`** — same pattern (one shared
   fixture across a test collection), but wraps the `MockHttpMessageHandler`
   + an `HttpClient` pointed at it. Justify in the report: "no local
   database in the client; equivalent pattern used for HTTP."

Recommend doing **both**: a real `DatabaseFixture` that proves the shared
data model is sound (so the grader sees it), plus an `ApiFixture` that's
actually used by most tests.

### Coverage

- Use `coverlet.collector` + `dotnet test --collect:"XPlat Code Coverage"`.
- Generate an HTML report with `reportgenerator`
  (`dotnet tool install -g dotnet-reportgenerator-globaltool`).
- Focus coverage on `Services/` and `Repositories/` first — ViewModels get
  to the target cheaply; the rubric will look for real logic tests, not
  property-setter tests.
- Realistic split to hit >60% without busywork:
  - Services: aim ≥90% (these are pure logic, trivial to test).
  - Repositories: aim ≥75%.
  - ViewModels: aim ≥50% (cover commands + error paths).
  - Exclude `Views/`, `App.xaml.cs`, `MauiProgram.cs`, generated code.

### Example test shapes

```csharp
// Service unit test — AAA
public class RentalServiceTests
{
    [Theory]
    [InlineData("Requested",  "Approved",  true)]
    [InlineData("Requested",  "Rejected",  true)]
    [InlineData("Approved",   "Completed", false)]   // must go via OutForRent
    [InlineData("Completed",  "Approved",  false)]
    public void CanTransition_returns_expected(string from, string to, bool expected)
    {
        // Arrange
        var svc = new RentalService(Mock.Of<IRentalRepository>());

        // Act
        var allowed = svc.CanTransition(Enum.Parse<RentalStatus>(from),
                                        Enum.Parse<RentalStatus>(to));

        // Assert
        allowed.Should().Be(expected);
    }
}

// Repository unit test — AAA with canned HTTP handler
public class HttpItemRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_parses_response()
    {
        // Arrange
        var handler = new MockHttpMessageHandler()
            .When(HttpMethod.Get, "https://api/items/42")
            .Respond("application/json",
                """{"id":42,"title":"Drill","dailyRate":5.50,...}""");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api/") };
        var repo = new HttpItemRepository(new FakeHttpFactory(client));

        // Act
        var item = await repo.GetByIdAsync(42);

        // Assert
        item!.Title.Should().Be("Drill");
        item.DailyRate.Should().Be(5.50m);
    }
}
```

---

## 8. Things to decide up front (they'll bite if left)

- **`RentalStatus` enum vs strings.** Use an enum in the client for
  compile-time safety; map to API strings via `JsonStringEnumConverter`.
  Probe the live API to confirm exact casing (`OutForRent` vs `out_for_rent`).
- **Date format.** API expects `yyyy-MM-dd`. Wrap in a converter so VMs
  exchange `DateOnly` and serialisation is a single concern.
- **JSON casing.** `PropertyNameCaseInsensitive = true` globally.
- **Money.** `decimal` with 2 dp everywhere. One currency app-wide (GBP).
- **Permissions on Android.** `CreateItemPage` / `NearbyItemsPage` need
  fine location. Request via `Permissions.RequestAsync<Permissions.LocationWhenInUse>()`
  inside `LocationService`. Add `ACCESS_FINE_LOCATION` to
  `Platforms/Android/AndroidManifest.xml`.
- **Error surfacing.** Every VM has a standard `try/catch` where
  `ApiException` becomes `SetError(ex.Message)`. Don't scatter error
  handling in services.
- **No `HttpClient` outside the repository + auth layer.** Grep for
  `HttpClient` after each slice — any hits in `ViewModels/` or `Views/` is
  a bug.

---

## 9. Android emulator build & deploy

Fast dev loop (emulator already running):

```bash
cd /Users/mattwalker/Projects/RentalApp/RentalApp
dotnet build -t:Run -f net10.0-android -c Debug
```

Produce an APK and install manually:

```bash
cd /Users/mattwalker/Projects/RentalApp/RentalApp
dotnet publish -f net10.0-android -c Debug \
  -p:AndroidPackageFormat=apk \
  -o ./publish

adb install -r ./publish/com.companyname.rentalapp-Signed.apk
adb shell monkey -p com.companyname.rentalapp 1   # launch
```

Emulator lifecycle:

```bash
~/Library/Android/sdk/emulator/emulator -list-avds
~/Library/Android/sdk/emulator/emulator -avd Pixel_7_API_34 &
adb devices
adb logcat | grep -i -E "rentalapp|mono-stdout|MAUI"
```

Notes:

- API is HTTPS, so no cleartext-traffic config needed.
- Android minimum is set to API 21 in `RentalApp.csproj`; `SecureStorage`
  and `Geolocation` work fine there.
- `10.0.2.2` is the emulator's alias for host localhost — irrelevant here
  since the API is hosted.

---

## 10. First three concrete steps

1. **Foundations (slice 1).** Create `Services/Http/` with `ITokenStore`,
   `SecureStorageTokenStore`, `AuthHeaderHandler`, `ApiException`. Rewire
   `MauiProgram.cs` to use `IHttpClientFactory`. Rebuild
   `ApiAuthenticationService` on top of the handler + token store. The app
   should look identical to the user after this — pure plumbing swap.
2. **Repository skeleton (slice 3 prep).** Add `Repositories/IRepository.cs`
   (generic), `ICategoryRepository` + `HttpCategoryRepository`,
   `IItemRepository` + `HttpItemRepository` (read paths only:
   `ListAsync`, `GetByIdAsync`, `SearchAsync`). Wire up in DI. No UI yet.
3. **Test project (slice 10 starts here, not last).** Create
   `RentalApp.Tests` with xUnit + Moq + FluentAssertions + coverlet. Write
   one test per layer (repository, service, viewmodel) as a template so
   future slices can copy-paste. Running `dotnet test` should report
   coverage.

Starting the test project in step 3 instead of leaving it to the end is a
deliberate choice: the test infrastructure pays for itself from slice 4
onward, and >60% coverage retrofitted at the end is painful.
