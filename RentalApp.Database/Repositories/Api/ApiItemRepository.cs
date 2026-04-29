using System.Net.Http.Json;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories.Api;

/// <summary>
/// <see cref="IItemRepository"/> backed by the hosted API.
/// </summary>
/// <remarks>
/// The hosted API uses three subtly different item shapes depending on the
/// endpoint, so this file declares three private wire records and a
/// per-shape mapper. Specifically:
/// <list type="bullet">
///   <item><description><c>GET /items</c> list items omit <c>latitude</c>/<c>longitude</c>.</description></item>
///   <item><description><c>GET /items/{id}</c> detail responses include them as nullable, plus <c>totalReviews</c> and an inline <c>reviews</c> array.</description></item>
///   <item><description><c>GET /items/nearby</c> items include <c>latitude</c>, <c>longitude</c>, and <c>distance</c>.</description></item>
///   <item><description><c>PUT /items/{id}</c> returns a slim 5-field response which is merged onto the caller's entity rather than replacing it.</description></item>
/// </list>
/// All wire records use nullable types where the spec marks the field nullable
/// or where it's absent on a particular endpoint, so deserialisation is
/// permissive.
/// </remarks>
public sealed class ApiItemRepository : IItemRepository
{
    private readonly HttpClient _http;

    public ApiItemRepository(HttpClient http) => _http = http;

    // ---- Generic CRUD -----------------------------------------------------

    public async Task<Item?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"items/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await response.EnsureSuccessOrThrowApiErrorAsync(ct);
        var wire = await response.Content.ReadFromJsonAsync<ItemDetailWire>(ApiJsonOptions.Default, ct);
        return wire is null ? null : ToModel(wire);
    }

    public async Task<IReadOnlyList<Item>> ListAsync(CancellationToken ct = default)
    {
        // Default first-page list — most callers should use SearchAsync for
        // anything paginated, but the generic contract requires this.
        var paged = await SearchAsync(new ItemQuery(), ct);
        return paged.Items;
    }

    public async Task<Item> CreateAsync(Item entity, CancellationToken ct = default)
    {
        var body = new CreateItemBody(
            Title: entity.Title,
            Description: entity.Description,
            DailyRate: entity.DailyRate,
            CategoryId: entity.CategoryId,
            Latitude: entity.Latitude,
            Longitude: entity.Longitude);

        var response = await _http.PostAsJsonAsync("items", body, ApiJsonOptions.Default, ct);
        await response.EnsureSuccessOrThrowApiErrorAsync(ct);

        // POST /items returns a "create response" shape which mostly overlaps
        // with the detail shape but doesn't carry reviews/totalReviews; reusing
        // ItemDetailWire is fine since those fields are nullable.
        var wire = await response.Content.ReadFromJsonAsync<ItemDetailWire>(ApiJsonOptions.Default, ct)
                   ?? throw new InvalidOperationException("Empty response body from POST /items.");
        return ToModel(wire);
    }

    public async Task<Item> UpdateAsync(Item entity, CancellationToken ct = default)
    {
        // PUT /items/{id} accepts a partial body: title/description/dailyRate/isAvailable.
        // CategoryId, latitude, longitude are not changeable via PUT — silently dropped.
        var body = new UpdateItemBody(
            Title: entity.Title,
            Description: entity.Description,
            DailyRate: entity.DailyRate,
            IsAvailable: entity.IsAvailable);

        var response = await _http.PutAsJsonAsync($"items/{entity.Id}", body, ApiJsonOptions.Default, ct);
        await response.EnsureSuccessOrThrowApiErrorAsync(ct);

        // The API returns a slim 5-field shape on PUT. Don't try to
        // reconstruct a full Item from it — instead merge the confirmed
        // fields onto the caller's entity (which already has the rest).
        var slim = await response.Content.ReadFromJsonAsync<UpdateItemResponseWire>(ApiJsonOptions.Default, ct)
                   ?? throw new InvalidOperationException("Empty response body from PUT /items/{id}.");

        entity.Id = slim.Id;
        entity.Title = slim.Title;
        entity.Description = slim.Description;
        entity.DailyRate = slim.DailyRate;
        entity.IsAvailable = slim.IsAvailable;
        return entity;
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose DELETE /items/{id}.");

    // ---- Specialised reads ------------------------------------------------

    public async Task<PagedResult<Item>> SearchAsync(ItemQuery query, CancellationToken ct = default)
    {
        var url = BuildItemListUrl(query);
        var wire = await _http.GetFromJsonAsync<ItemListEnvelopeWire>(url, ApiJsonOptions.Default, ct);
        return wire is null
            ? PagedResult<Item>.Empty(query.Page, query.PageSize)
            : new PagedResult<Item>
            {
                Items = (wire.Items ?? Array.Empty<ItemListItemWire>()).Select(ToModel).ToList(),
                Page = wire.Page ?? query.Page,
                PageSize = wire.PageSize ?? query.PageSize,
                TotalCount = wire.TotalItems ?? 0,
            };
    }

    public async Task<IReadOnlyList<Item>> GetNearbyAsync(
        double lat,
        double lon,
        double radiusKm,
        string? categorySlug = null,
        CancellationToken ct = default)
    {
        var parts = new List<string>
        {
            $"lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"radius={radiusKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        };
        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            parts.Add($"category={Uri.EscapeDataString(categorySlug)}");
        }
        var url = "items/nearby?" + string.Join("&", parts);

        // Spec wraps the array: { items, searchLocation, radius, totalResults }.
        var wire = await _http.GetFromJsonAsync<NearbyEnvelopeWire>(url, ApiJsonOptions.Default, ct);
        return (wire?.Items ?? Array.Empty<NearbyItemWire>()).Select(ToModel).ToList();
    }

    public async Task<PagedResult<Review>> GetReviewsAsync(
        int itemId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var url = $"items/{itemId}/reviews?page={page}&pageSize={pageSize}";
        // Spec keys are { reviews, averageRating, totalReviews, page, pageSize, totalPages }.
        var wire = await _http.GetFromJsonAsync<ReviewListEnvelopeWire>(url, ApiJsonOptions.Default, ct);
        return wire is null
            ? PagedResult<Review>.Empty(page, pageSize)
            : new PagedResult<Review>
            {
                Items = (wire.Reviews ?? Array.Empty<ItemReviewWire>()).Select(ToReviewModel).ToList(),
                Page = wire.Page ?? page,
                PageSize = wire.PageSize ?? pageSize,
                TotalCount = wire.TotalReviews ?? 0,
            };
    }

    // ---- URL building ------------------------------------------------------

    private static string BuildItemListUrl(ItemQuery q)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(q.CategorySlug))
            parts.Add($"category={Uri.EscapeDataString(q.CategorySlug)}");
        if (!string.IsNullOrWhiteSpace(q.Search))
            parts.Add($"search={Uri.EscapeDataString(q.Search)}");
        parts.Add($"page={q.Page}");
        parts.Add($"pageSize={q.PageSize}");
        return "items?" + string.Join("&", parts);
    }

    // ---- Wire records (private) -------------------------------------------

    /// <summary>
    /// Shape returned by <c>GET /items/{id}</c> and <c>POST /items</c>.
    /// Latitude/longitude are nullable because the API marks them so; the
    /// inline reviews list and total-reviews count are present on detail GET
    /// only.
    /// </summary>
    private sealed record ItemDetailWire(
        int Id,
        string Title,
        string? Description,
        decimal DailyRate,
        int CategoryId,
        string? Category,
        int OwnerId,
        string? OwnerName,
        double? OwnerRating,
        double? Latitude,
        double? Longitude,
        bool IsAvailable,
        double? AverageRating,
        int? TotalReviews,
        DateTime CreatedAt,
        IReadOnlyList<InlineReviewWire>? Reviews);

    /// <summary>
    /// Shape returned inside the <c>GET /items</c> list. No latitude/longitude.
    /// </summary>
    private sealed record ItemListItemWire(
        int Id,
        string Title,
        string? Description,
        decimal DailyRate,
        int CategoryId,
        string? Category,
        int OwnerId,
        string? OwnerName,
        double? OwnerRating,
        bool IsAvailable,
        double? AverageRating,
        DateTime CreatedAt);

    /// <summary>
    /// Envelope around the <c>GET /items</c> list.
    /// </summary>
    private sealed record ItemListEnvelopeWire(
        IReadOnlyList<ItemListItemWire>? Items,
        int? TotalItems,
        int? Page,
        int? PageSize,
        int? TotalPages);

    /// <summary>
    /// Shape returned inside <c>GET /items/nearby</c>. Has latitude/longitude
    /// and a <c>distance</c> field (in km).
    /// </summary>
    private sealed record NearbyItemWire(
        int Id,
        string Title,
        string? Description,
        decimal DailyRate,
        int CategoryId,
        string? Category,
        int OwnerId,
        string? OwnerName,
        double Latitude,
        double Longitude,
        double Distance,
        bool IsAvailable,
        double? AverageRating);

    /// <summary>
    /// Envelope around <c>GET /items/nearby</c>:
    /// <c>{ items, searchLocation, radius, totalResults }</c>.
    /// </summary>
    private sealed record NearbyEnvelopeWire(
        IReadOnlyList<NearbyItemWire>? Items,
        SearchLocationWire? SearchLocation,
        double? Radius,
        int? TotalResults);

    private sealed record SearchLocationWire(double Latitude, double Longitude);

    /// <summary>
    /// Inline review on <c>GET /items/{id}</c>. We don't surface these to the
    /// model directly — the dedicated paged <c>GET /items/{id}/reviews</c>
    /// endpoint is what <see cref="GetReviewsAsync"/> uses. Kept as a private
    /// type only so the detail wire can deserialise the field cleanly.
    /// </summary>
    private sealed record InlineReviewWire(
        int Id,
        int ReviewerId,
        string? ReviewerName,
        int Rating,
        string? Comment,
        DateTime CreatedAt);

    /// <summary>
    /// Item review wire used by <c>GET /items/{id}/reviews</c>. Extended
    /// vs. <see cref="InlineReviewWire"/> because the dedicated endpoint also
    /// includes <c>rentalId</c>.
    /// </summary>
    private sealed record ItemReviewWire(
        int Id,
        int? RentalId,
        int ReviewerId,
        string? ReviewerName,
        int Rating,
        string? Comment,
        DateTime CreatedAt);

    /// <summary>
    /// Paged-reviews envelope used on item and user reviews endpoints:
    /// <c>{ reviews, averageRating, totalReviews, page, pageSize, totalPages }</c>.
    /// </summary>
    private sealed record ReviewListEnvelopeWire(
        IReadOnlyList<ItemReviewWire>? Reviews,
        double? AverageRating,
        int? TotalReviews,
        int? Page,
        int? PageSize,
        int? TotalPages);

    // Outgoing bodies. Property names are PascalCase; ApiJsonOptions handles
    // the camelCase conversion on the wire.
    private sealed record CreateItemBody(
        string Title,
        string? Description,
        decimal DailyRate,
        int CategoryId,
        double Latitude,
        double Longitude);

    private sealed record UpdateItemBody(
        string? Title,
        string? Description,
        decimal? DailyRate,
        bool? IsAvailable);

    /// <summary>
    /// Slim PUT response shape — only the fields the API echoes back.
    /// </summary>
    private sealed record UpdateItemResponseWire(
        int Id,
        string Title,
        string? Description,
        decimal DailyRate,
        bool IsAvailable);

    // ---- Mappers ----------------------------------------------------------

    private static Item ToModel(ItemDetailWire w) => new()
    {
        Id = w.Id,
        Title = w.Title,
        Description = w.Description,
        DailyRate = w.DailyRate,
        CategoryId = w.CategoryId,
        OwnerId = w.OwnerId,
        Latitude = w.Latitude ?? 0.0,
        Longitude = w.Longitude ?? 0.0,
        IsAvailable = w.IsAvailable,
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.CreatedAt,

        OwnerName = w.OwnerName ?? string.Empty,
        OwnerRating = w.OwnerRating,
        AverageRating = w.AverageRating,
        CategoryName = w.Category ?? string.Empty,
        CategorySlug = string.Empty,
        DistanceKm = null,

        // Phase 7b: surface the inline reviews from the wire so the page can
        // render them without an extra round-trip to GET /items/{id}/reviews.
        TotalReviews = w.TotalReviews ?? 0,
        Reviews = (w.Reviews ?? Array.Empty<InlineReviewWire>())
            .Select(r => new Review
            {
                Id = r.Id,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.ReviewerName ?? string.Empty,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
            })
            .ToList(),
    };

    private static Item ToModel(ItemListItemWire w) => new()
    {
        Id = w.Id,
        Title = w.Title,
        Description = w.Description,
        DailyRate = w.DailyRate,
        CategoryId = w.CategoryId,
        OwnerId = w.OwnerId,
        Latitude = 0.0,
        Longitude = 0.0,
        IsAvailable = w.IsAvailable,
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.CreatedAt,

        OwnerName = w.OwnerName ?? string.Empty,
        OwnerRating = w.OwnerRating,
        AverageRating = w.AverageRating,
        CategoryName = w.Category ?? string.Empty,
        CategorySlug = string.Empty,
        DistanceKm = null,
    };

    private static Item ToModel(NearbyItemWire w) => new()
    {
        Id = w.Id,
        Title = w.Title,
        Description = w.Description,
        DailyRate = w.DailyRate,
        CategoryId = w.CategoryId,
        OwnerId = w.OwnerId,
        Latitude = w.Latitude,
        Longitude = w.Longitude,
        IsAvailable = w.IsAvailable,
        CreatedAt = DateTime.UtcNow, // not on the wire for nearby
        UpdatedAt = DateTime.UtcNow,

        OwnerName = w.OwnerName ?? string.Empty,
        OwnerRating = null,
        AverageRating = w.AverageRating,
        CategoryName = w.Category ?? string.Empty,
        CategorySlug = string.Empty,
        DistanceKm = w.Distance,
    };

    private static Review ToReviewModel(ItemReviewWire w) => new()
    {
        Id = w.Id,
        RentalId = w.RentalId ?? 0,
        ReviewerId = w.ReviewerId,
        Rating = w.Rating,
        Comment = w.Comment,
        CreatedAt = w.CreatedAt,
        ReviewerName = w.ReviewerName ?? string.Empty,
        ItemTitle = string.Empty,
    };
}
