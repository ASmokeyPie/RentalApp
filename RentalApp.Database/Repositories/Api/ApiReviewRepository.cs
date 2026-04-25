using System.Net.Http.Json;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories.Api;

/// <summary>
/// <see cref="IReviewRepository"/> backed by the hosted API. The API exposes
/// <c>POST /reviews</c> for creation and two paginated read endpoints
/// (<c>GET /items/{id}/reviews</c>, <c>GET /users/{id}/reviews</c>); generic
/// CRUD methods that don't map throw <see cref="NotSupportedException"/>.
/// </summary>
public sealed class ApiReviewRepository : IReviewRepository
{
    private readonly HttpClient _http;

    public ApiReviewRepository(HttpClient http) => _http = http;

    // ---- Generic CRUD -----------------------------------------------------

    public Task<Review?> GetByIdAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "The hosted API has no GET /reviews/{id} — fetch via GetForItemAsync or GetForUserAsync.");

    public Task<IReadOnlyList<Review>> ListAsync(CancellationToken ct = default) =>
        throw new NotSupportedException(
            "The hosted API has no flat /reviews list — fetch by item or user.");

    /// <summary>
    /// Creates a review from a fully-populated <see cref="Review"/>.
    /// Equivalent to calling the more explicit
    /// <see cref="CreateAsync(int,int,string?,CancellationToken)"/> overload.
    /// </summary>
    public Task<Review> CreateAsync(Review entity, CancellationToken ct = default) =>
        CreateAsync(entity.RentalId, entity.Rating, entity.Comment, ct);

    public Task<Review> UpdateAsync(Review entity, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose PUT /reviews/{id}.");

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose DELETE /reviews/{id}.");

    // ---- Specialised methods ----------------------------------------------

    public async Task<Review> CreateAsync(int rentalId, int rating, string? comment, CancellationToken ct = default)
    {
        var body = new CreateReviewBody(rentalId, rating, comment);
        var response = await _http.PostAsJsonAsync("reviews", body, ApiJsonOptions.Default, ct);
        response.EnsureSuccessStatusCode();
        var wire = await response.Content.ReadFromJsonAsync<ReviewWire>(ApiJsonOptions.Default, ct)
                   ?? throw new InvalidOperationException("Empty response body from POST /reviews.");
        return ToModel(wire);
    }

    public Task<PagedResult<Review>> GetForUserAsync(int userId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        GetPagedAsync($"users/{userId}/reviews?page={page}&pageSize={pageSize}", page, pageSize, ct);

    public Task<PagedResult<Review>> GetForItemAsync(int itemId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        GetPagedAsync($"items/{itemId}/reviews?page={page}&pageSize={pageSize}", page, pageSize, ct);

    private async Task<PagedResult<Review>> GetPagedAsync(string url, int page, int pageSize, CancellationToken ct)
    {
        // Spec envelope: { reviews, averageRating, totalReviews, page, pageSize, totalPages }.
        var wire = await _http.GetFromJsonAsync<ReviewListEnvelopeWire>(url, ApiJsonOptions.Default, ct);
        return wire is null
            ? PagedResult<Review>.Empty(page, pageSize)
            : new PagedResult<Review>
            {
                Items = (wire.Reviews ?? Array.Empty<ReviewWire>()).Select(ToModel).ToList(),
                Page = wire.Page ?? page,
                PageSize = wire.PageSize ?? pageSize,
                TotalCount = wire.TotalReviews ?? 0,
            };
    }

    // ---- Wire records -----------------------------------------------------

    /// <summary>
    /// Review wire shape. <c>RentalId</c> is required on the POST response
    /// but absent on the user-profile review shape; <c>ItemTitle</c> isn't on
    /// the spec — we keep it nullable so future API additions don't break.
    /// </summary>
    private sealed record ReviewWire(
        int Id,
        int? RentalId,
        int ReviewerId,
        string? ReviewerName,
        string? ItemTitle,
        int Rating,
        string? Comment,
        DateTime CreatedAt);

    /// <summary>
    /// Paged-reviews envelope used on item and user reviews endpoints:
    /// <c>{ reviews, averageRating, totalReviews, page, pageSize, totalPages }</c>.
    /// </summary>
    private sealed record ReviewListEnvelopeWire(
        IReadOnlyList<ReviewWire>? Reviews,
        double? AverageRating,
        int? TotalReviews,
        int? Page,
        int? PageSize,
        int? TotalPages);

    private sealed record CreateReviewBody(int RentalId, int Rating, string? Comment);

    // ---- Mapper -----------------------------------------------------------

    private static Review ToModel(ReviewWire w) => new()
    {
        Id = w.Id,
        RentalId = w.RentalId,
        ReviewerId = w.ReviewerId,
        Rating = w.Rating,
        Comment = w.Comment,
        CreatedAt = w.CreatedAt,
        ReviewerName = w.ReviewerName ?? string.Empty,
        ItemTitle = w.ItemTitle ?? string.Empty,
    };
}
