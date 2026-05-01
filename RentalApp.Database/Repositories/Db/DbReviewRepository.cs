using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Services;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="IReviewRepository"/>.
/// Each method creates its own short-lived <see cref="AppDbContext"/> via the
/// factory so concurrent calls never share a context instance.
/// </summary>
public sealed class DbReviewRepository : IReviewRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUserProvider _currentUser;

    public DbReviewRepository(IDbContextFactory<AppDbContext> factory, ICurrentUserProvider currentUser)
    {
        _factory     = factory;
        _currentUser = currentUser;
    }

    // ---- Generic CRUD (not supported) -------------------------------------

    public Task<Review?> GetByIdAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("There is no GET /reviews/{id} — fetch via GetForItemAsync or GetForUserAsync.");

    public Task<IReadOnlyList<Review>> ListAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("There is no flat reviews list — fetch by item or user.");

    public Task<Review> UpdateAsync(Review entity, CancellationToken ct = default) =>
        throw new NotSupportedException("Reviews cannot be updated after creation.");

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("Reviews cannot be deleted.");

    // ---- Create -----------------------------------------------------------

    /// <summary>
    /// Convenience overload: creates from a populated <see cref="Review"/> entity.
    /// </summary>
    public Task<Review> CreateAsync(Review entity, CancellationToken ct = default) =>
        CreateAsync(entity.RentalId, entity.Rating, entity.Comment, ct);

    /// <summary>
    /// Creates a review for a completed rental. The reviewer is the currently
    /// authenticated user (must be the borrower on the rental).
    /// </summary>
    public async Task<Review> CreateAsync(int rentalId, int rating, string? comment, CancellationToken ct = default)
    {
        var reviewerId = _currentUser.CurrentUserId
            ?? throw new InvalidOperationException("No authenticated user — cannot create a review.");

        using var db = _factory.CreateDbContext();

        // Verify the rental exists and load the item title for display fields.
        var rental = await db.Rentals
            .Include(r => r.Item)
            .Include(r => r.Borrower)
            .FirstOrDefaultAsync(r => r.Id == rentalId, ct)
            ?? throw new InvalidOperationException($"Rental {rentalId} not found.");

        var review = new Review
        {
            RentalId   = rentalId,
            ReviewerId = reviewerId,
            Rating     = rating,
            Comment    = comment,
            CreatedAt  = DateTime.UtcNow,
        };

        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);

        // Populate display fields from the loaded graph.
        review.ItemTitle = rental.Item?.Title ?? string.Empty;
        if (rental.Borrower is not null)
            review.ReviewerName = FormatDisplayName(rental.Borrower.FirstName, rental.Borrower.LastName);

        return review;
    }

    // ---- Reads ------------------------------------------------------------

    /// <summary>
    /// Returns a page of reviews written by the given user, ordered newest-first.
    /// Populates <see cref="Review.ItemTitle"/> from the rental's item.
    /// </summary>
    public async Task<PagedResult<Review>> GetForUserAsync(
        int userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();

        var q = db.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Rental!)
                .ThenInclude(rental => rental.Item)
            .Where(r => r.ReviewerId == userId);

        var total = await q.CountAsync(ct);

        var reviews = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        foreach (var r in reviews)
            PopulateDisplayFields(r);

        return new PagedResult<Review>
        {
            Items      = reviews,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total,
        };
    }

    /// <summary>
    /// Returns a page of reviews for the given item, ordered newest-first.
    /// Populates <see cref="Review.ReviewerName"/> and <see cref="Review.ItemTitle"/>.
    /// </summary>
    public async Task<PagedResult<Review>> GetForItemAsync(
        int itemId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();

        var q = db.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Rental!)
                .ThenInclude(rental => rental.Item)
            .Where(r => r.Rental!.ItemId == itemId);

        var total = await q.CountAsync(ct);

        var reviews = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        foreach (var r in reviews)
            PopulateDisplayFields(r);

        return new PagedResult<Review>
        {
            Items      = reviews,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total,
        };
    }

    // ---- Helpers ----------------------------------------------------------

    private static void PopulateDisplayFields(Review review)
    {
        if (review.Reviewer is not null)
            review.ReviewerName = FormatDisplayName(
                review.Reviewer.FirstName, review.Reviewer.LastName);

        if (review.Rental?.Item is not null)
            review.ItemTitle = review.Rental.Item.Title;
    }

    private static string FormatDisplayName(string firstName, string lastName) =>
        string.IsNullOrEmpty(lastName) ? firstName : $"{firstName} {lastName[0]}.";
}
