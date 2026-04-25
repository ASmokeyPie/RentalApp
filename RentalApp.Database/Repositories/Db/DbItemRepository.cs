using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="IItemRepository"/>.
/// Stubbed for now — every method throws <see cref="NotImplementedException"/>.
/// Will be filled in when the local-DB path comes back online (the
/// <c>useSharedApi=false</c> branch of <c>MauiProgram.CreateMauiApp</c>).
/// </summary>
public sealed class DbItemRepository : IItemRepository
{
    public Task<Item?> GetByIdAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Item>> ListAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Item> CreateAsync(Item entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Item> UpdateAsync(Item entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PagedResult<Item>> SearchAsync(ItemQuery query, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Item>> GetNearbyAsync(
        double lat, double lon, double radiusKm, string? categorySlug = null, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PagedResult<Review>> GetReviewsAsync(int itemId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
