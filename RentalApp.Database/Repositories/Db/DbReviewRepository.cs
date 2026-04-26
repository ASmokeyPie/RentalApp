using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="IReviewRepository"/>.
/// Stubbed — see remarks on <see cref="DbItemRepository"/>.
/// </summary>
public sealed class DbReviewRepository : IReviewRepository
{
    public Task<Review?> GetByIdAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Review>> ListAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Review> CreateAsync(Review entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Review> CreateAsync(int rentalId, int rating, string? comment, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Review> UpdateAsync(Review entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PagedResult<Review>> GetForUserAsync(int userId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PagedResult<Review>> GetForItemAsync(int itemId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
