using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories;

/// <summary>
/// Data access for <see cref="Item"/>. Wraps <c>GET /items</c>,
/// <c>GET /items/{id}</c>, <c>POST /items</c>, <c>PUT /items/{id}</c> and
/// <c>GET /items/nearby</c>, plus <c>GET /items/{id}/reviews</c> for the
/// embedded-reviews paged response.
/// </summary>
/// <remarks>
/// Inherits the generic CRUD shape from <see cref="IRepository{T}"/>.
/// <c>DeleteAsync</c> is unsupported on the hosted API — the implementation
/// throws <see cref="NotSupportedException"/>; callers should not invoke it.
/// </remarks>
public interface IItemRepository : IRepository<Item>
{
    /// <summary>Paginated, filtered search across all items.</summary>
    Task<PagedResult<Item>> SearchAsync(ItemQuery query, CancellationToken ct = default);

    /// <summary>Items within <paramref name="radiusKm"/> of (<paramref name="lat"/>, <paramref name="lon"/>), optionally filtered by category slug.</summary>
    Task<IReadOnlyList<Item>> GetNearbyAsync(
        double lat,
        double lon,
        double radiusKm,
        string? categorySlug = null,
        CancellationToken ct = default);

    /// <summary>Reviews left on the given item (paginated).</summary>
    Task<PagedResult<Review>> GetReviewsAsync(
        int itemId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
