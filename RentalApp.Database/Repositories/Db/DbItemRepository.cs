using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Services;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="IItemRepository"/>.
/// Each method creates its own short-lived <see cref="AppDbContext"/> via the
/// factory so concurrent calls never share a context instance.
/// All queries eager-load Owner and Category so the <c>[NotMapped]</c> display
/// fields (<see cref="Item.OwnerName"/>, <see cref="Item.CategoryName"/>, etc.)
/// can be populated without extra round-trips.
/// <see cref="GetNearbyAsync"/> is left throwing until Stage 4 (PostGIS).
/// </summary>
public sealed class DbItemRepository : IItemRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUserProvider _currentUser;

    public DbItemRepository(IDbContextFactory<AppDbContext> factory, ICurrentUserProvider currentUser)
    {
        _factory     = factory;
        _currentUser = currentUser;
    }

    // ---- Base CRUD --------------------------------------------------------

    public async Task<Item?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        var item = await db.Items
            .Include(i => i.Owner)
            .Include(i => i.Category)
            .Include(i => i.Rentals)
                .ThenInclude(r => r.Review)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (item is null) return null;

        PopulateDisplayFields(item);

        // Compute aggregate review stats from the loaded graph.
        var reviews = item.Rentals
            .Where(r => r.Review is not null)
            .Select(r => r.Review!)
            .ToList();

        item.AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : null;
        item.TotalReviews  = reviews.Count;
        item.Reviews       = reviews;

        return item;
    }

    public async Task<IReadOnlyList<Item>> ListAsync(CancellationToken ct = default)
        => (await SearchAsync(new ItemQuery(), ct)).Items;

    public async Task<Item> CreateAsync(Item entity, CancellationToken ct = default)
    {
        // The ViewModel doesn't set OwnerId (the API infers it from the JWT).
        // For the DB path we stamp it here from the authenticated user.
        entity.OwnerId   = _currentUser.CurrentUserId
            ?? throw new InvalidOperationException("Cannot create an item: no authenticated user.");
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        using var db = _factory.CreateDbContext();
        db.Items.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Item> UpdateAsync(Item entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        using var db = _factory.CreateDbContext();
        db.Items.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        var item = await db.Items.FindAsync(new object[] { id }, ct);
        if (item is not null)
        {
            db.Items.Remove(item);
            await db.SaveChangesAsync(ct);
        }
    }

    // ---- Specialised reads ------------------------------------------------

    public async Task<PagedResult<Item>> SearchAsync(ItemQuery query, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        var q = db.Items
            .Include(i => i.Owner)
            .Include(i => i.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
            q = q.Where(i => i.Category!.Slug == query.CategorySlug);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(i =>
                EF.Functions.ILike(i.Title, $"%{query.Search}%") ||
                (i.Description != null && EF.Functions.ILike(i.Description, $"%{query.Search}%")));

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(i => i.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        foreach (var item in items)
            PopulateDisplayFields(item);

        return new PagedResult<Item>
        {
            Items     = items,
            Page      = query.Page,
            PageSize  = query.PageSize,
            TotalCount = total,
        };
    }

    /// <summary>
    /// Not implemented until Stage 4 — requires the PostGIS extension and the
    /// <c>Location GEOGRAPHY(POINT,4326)</c> column added by that migration.
    /// </summary>
    public Task<IReadOnlyList<Item>> GetNearbyAsync(
        double lat, double lon, double radiusKm,
        string? categorySlug = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("GetNearbyAsync requires PostGIS — implement in Stage 4.");

    public async Task<PagedResult<Review>> GetReviewsAsync(
        int itemId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        // Filter through the Rental navigation without loading it; EF generates
        // an implicit JOIN. Include Reviewer so we can populate ReviewerName.
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

        foreach (var review in reviews)
            PopulateReviewDisplayFields(review);

        return new PagedResult<Review>
        {
            Items      = reviews,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total,
        };
    }

    // ---- Helpers ----------------------------------------------------------

    private static void PopulateDisplayFields(Item item)
    {
        if (item.Owner is not null)
            item.OwnerName = FormatDisplayName(item.Owner.FirstName, item.Owner.LastName);

        if (item.Category is not null)
        {
            item.CategoryName = item.Category.Name;
            item.CategorySlug = item.Category.Slug;
        }
    }

    private static void PopulateReviewDisplayFields(Review review)
    {
        if (review.Reviewer is not null)
            review.ReviewerName = FormatDisplayName(
                review.Reviewer.FirstName, review.Reviewer.LastName);

        if (review.Rental?.Item is not null)
            review.ItemTitle = review.Rental.Item.Title;
    }

    /// <summary>Formats "FirstName L." to match the API's denormalised display name.</summary>
    private static string FormatDisplayName(string firstName, string lastName) =>
        string.IsNullOrEmpty(lastName)
            ? firstName
            : $"{firstName} {lastName[0]}.";
}
