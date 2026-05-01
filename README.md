# RentalApp

A .NET MAUI Android app for peer-to-peer item rental — a "Library of Things" where users can list items they own, browse and rent items from others, manage the rental lifecycle, and leave reviews.

The app supports two fully interchangeable data backends controlled by a single flag in `MauiProgram.cs`:

- **Shared API mode** (`useSharedApi = true`) — connects to a hosted REST API with JWT authentication
- **Local DB mode** (`useSharedApi = false`) — runs entirely on-device against a local PostgreSQL database via EF Core, with BCrypt password hashing and no network dependency

---

## Features

- Register and log in (JWT via API, or BCrypt local auth)
- Browse and search items by category or keyword
- View item details with reviews and average rating
- List, edit, and delete your own items
- Location-based item discovery using PostGIS proximity queries
- Request rentals and manage the full rental lifecycle (Requested → Approved → Out for Rent → Returned → Completed)
- Overdue detection: rentals past their end date are automatically surfaced as Overdue
- Write reviews on completed rentals
- User profile with average owner rating

---

## Solution structure

```
RentalApp/                  .NET MAUI Android app (views, view models, services)
RentalApp.Database/         Class library — EF Core models, migrations, repositories
  ├── Models/               User, Item, Category, Rental, Review
  ├── Repositories/Api/     HTTP implementations (ApiItemRepository, etc.)
  ├── Repositories/Db/      EF Core implementations (DbItemRepository, etc.)
  ├── Helpers/              RentalStatusHelper (shared DeriveStatus logic)
  └── Migrations/           EF Core migration history
RentalApp.Migrations/       Console app that applies migrations (used by Docker Compose)
RentalApp.Tests/            xUnit test suite
  ├── Repositories/         API repository tests (HTTP stubs) + DB integration tests
  ├── Services/             Service and state-machine tests
  └── ViewModels/           ViewModel unit tests
```

---

## Tech stack

| Concern | Choice |
|---|---|
| UI framework | .NET MAUI (Android) |
| Architecture | MVVM — CommunityToolkit.Mvvm |
| Local database | PostgreSQL 16 + PostGIS 3.4 (via Docker) |
| ORM | Entity Framework Core 10 |
| Spatial queries | NetTopologySuite + Npgsql PostGIS extension |
| Password hashing | BCrypt.Net-Next |
| Testing | xUnit + Moq |
| CI | GitHub Actions |
| Secret scanning | gitleaks |

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Android emulator or physical device (API 21+)
- An IDE: [Rider](https://www.jetbrains.com/rider/), [Visual Studio](https://visualstudio.microsoft.com/), or [VS Code](https://code.visualstudio.com/)

### 1. Clone and configure

```bash
git clone <repo-url>
cd RentalApp

# Create your local environment file from the example
cp .env.example .env
# Edit .env and set POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
```

### 2. Start the database and run migrations

```bash
docker compose up migrate
```

This starts PostgreSQL with PostGIS, waits for it to be healthy, then runs all EF Core migrations including `CREATE EXTENSION IF NOT EXISTS postgis`.

Leave the database running in the background:

```bash
docker compose up -d db
```

### 3. Choose a backend mode

Open `RentalApp/MauiProgram.cs` and set the flag at the top of `CreateMauiApp`:

```csharp
// true  → hosted REST API (requires network, JWT auth)
// false → local PostgreSQL (requires docker compose up -d db)
const bool useSharedApi = false;
```

### 4. Run on an emulator

```bash
dotnet build RentalApp/RentalApp.csproj
# Deploy via your IDE, or:
dotnet run --project RentalApp/RentalApp.csproj -f net10.0-android
```

---

## API reference

When running in shared API mode (`useSharedApi = true`) the app targets the hosted SET09102 "Library of Things" API.

**Base URL:** `https://set09102-api.b-davison.workers.dev/`

| Resource | Endpoints |
|---|---|
| Auth | `POST /auth/register`, `POST /auth/login`, `GET /users/me` |
| Items | `GET /items`, `GET /items/{id}`, `POST /items`, `PUT /items/{id}`, `DELETE /items/{id}`, `GET /items/nearby`, `GET /items/{id}/reviews` |
| Rentals | `POST /rentals`, `GET /rentals/{id}`, `GET /rentals/incoming`, `GET /rentals/outgoing`, `PATCH /rentals/{id}/status` |
| Reviews | `POST /reviews`, `GET /users/{id}/reviews` |

Full interactive documentation: [https://set09102-api.b-davison.workers.dev/#/](https://set09102-api.b-davison.workers.dev/#/)

---

## Running the tests

The test suite includes unit tests (no dependencies) and integration tests that require the local database. Both run with a single command:

```bash
# Ensure the database is running first
docker compose up -d db

dotnet test RentalApp.Tests/RentalApp.Tests.csproj
```

Integration tests connect via the `TEST_CONNECTION_STRING` environment variable. If it isn't set, they fall back to the default values from `.env.example`. To use different credentials locally:

```bash
export TEST_CONNECTION_STRING="Host=localhost;Username=...;Password=...;Database=..."
dotnet test RentalApp.Tests/RentalApp.Tests.csproj
```

---

## Database migrations

Migrations live in `RentalApp.Database/Migrations/` and are applied by the `RentalApp.Migrations` console app.

**Add a new migration** (from inside the dev container, or with EF tools installed locally):

```bash
dotnet ef migrations add <MigrationName> \
  --project RentalApp.Database \
  --startup-project RentalApp.Migrations
```

**Apply migrations manually:**

```bash
docker compose up migrate
```

---

## CI

GitHub Actions runs on every push and pull request to `main` and `develop`:

1. **Secret scan** — gitleaks scans the full commit history; the build fails if any secrets are detected
2. **Build & test** — restores, builds all projects, runs the full test suite against an ephemeral PostGIS container

Workflow: `.github/workflows/ci.yml`

---

## Architecture notes

### Dual-backend pattern

Both backends implement the same repository interfaces (`IItemRepository`, `IRentalRepository`, etc.). `MauiProgram.cs` registers either the `Api*` or `Db*` implementations depending on `useSharedApi`. All view models and services are data-source-agnostic.

### DbContext lifetime in MAUI

MAUI's DI container has no HTTP request scope, so `AddDbContext` behaves like `AddSingleton` and causes concurrency errors when multiple pages are active. The local DB path uses `AddDbContextFactory<AppDbContext>()` instead, and every repository method creates and disposes its own short-lived context.

### Overdue status

`RentalStatus.Overdue` is a client-side derived state — it is never stored in the database. Reads elevate `OutForRent` rentals whose `EndDate` has passed; writes map `Overdue` back to `OutForRent` before persisting. The shared logic lives in `RentalApp.Database/Helpers/RentalStatusHelper.cs`.

### PostGIS proximity queries

Items store a `geography(Point, 4326)` column populated from `Latitude`/`Longitude` on save. The nearby-items query uses `ST_DWithin` (via `IsWithinDistance`) for fast indexed radius filtering and `ST_Distance` (via `Distance`) to compute kilometres for display.
