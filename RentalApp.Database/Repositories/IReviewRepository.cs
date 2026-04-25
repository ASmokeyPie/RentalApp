using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories;

/// <summary>
/// Data access for <see cref="Review"/>. The API offers a single create
/// endpoint (<c>POST /reviews</c>) and two paginated read endpoints
/// (<c>GET /items/{id}/reviews</c>, <c>GET /users/{id}/reviews</c>).
/// </summary>
public interface IReviewRepository : IRepository<Review>
{
    /// <summary>Create a review against a completed rental. Server returns 409 if a review already exists for that rental.</summary>
    Task<Review> CreateAsync(int rentalId, int rating, string? comment, CancellationToken ct = default);

    /// <summary>Paginated reviews for a given user.</summary>
    Task<PagedResult<Review>> GetForUserAsync(int userId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Paginated reviews for a given item. Identical to
    /// <see cref="IItemRepository.GetReviewsAsync"/> but kept here so review-centric
    /// flows can depend on a single repository.
    /// </summary>
    Task<PagedResult<Review>> GetForItemAsync(int itemId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
