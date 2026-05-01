using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Helpers;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Services;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="IRentalRepository"/>.
/// Each method creates its own short-lived <see cref="AppDbContext"/> via the
/// factory so concurrent calls never share a context instance.
///
/// Status persistence note: <see cref="RentalStatus.Overdue"/> is a client-side
/// derived state (OutForRent whose EndDate has passed). It is never written to the
/// database — reads elevate OutForRent → Overdue via <see cref="RentalStatusHelper.DeriveStatus"/>,
/// and writes map Overdue back to OutForRent before calling SaveChangesAsync.
/// </summary>
public sealed class DbRentalRepository : IRentalRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUserProvider _currentUser;

    public DbRentalRepository(IDbContextFactory<AppDbContext> factory, ICurrentUserProvider currentUser)
    {
        _factory     = factory;
        _currentUser = currentUser;
    }

    // ---- Generic CRUD (not supported for rentals) -------------------------

    public Task<IReadOnlyList<Rental>> ListAsync(CancellationToken ct = default) =>
        throw new NotSupportedException(
            "There is no flat rentals list — call GetIncomingAsync or GetOutgoingAsync.");

    public Task<Rental> CreateAsync(Rental entity, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Use RequestAsync(itemId, startDate, endDate); a Rental cannot be created wholesale.");

    public Task<Rental> UpdateAsync(Rental entity, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Use UpdateStatusAsync; there is no wholesale rental update.");

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("Rentals cannot be deleted.");

    // ---- Reads ------------------------------------------------------------

    public async Task<Rental?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        var rental = await db.Rentals
            .Include(r => r.Item!)
                .ThenInclude(i => i.Owner)
            .Include(r => r.Item!)
                .ThenInclude(i => i.Category)
            .Include(r => r.Borrower)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rental is null) return null;

        PopulateDisplayFields(rental);
        rental.Status = RentalStatusHelper.DeriveStatus(rental.Status, rental.EndDate);
        return rental;
    }

    public async Task<IReadOnlyList<Rental>> GetIncomingAsync(RentalQuery? query = null, CancellationToken ct = default)
    {
        var currentUserId = RequireCurrentUserId();

        using var db = _factory.CreateDbContext();
        var q = db.Rentals
            .Include(r => r.Item!)
                .ThenInclude(i => i.Owner)
            .Include(r => r.Item!)
                .ThenInclude(i => i.Category)
            .Include(r => r.Borrower)
            .Where(r => r.Item!.OwnerId == currentUserId)
            .AsQueryable();

        q = ApplyStatusFilter(q, query?.Status);

        var rentals = await q
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        foreach (var r in rentals)
        {
            PopulateDisplayFields(r);
            r.Status = RentalStatusHelper.DeriveStatus(r.Status, r.EndDate);
        }

        return rentals;
    }

    public async Task<IReadOnlyList<Rental>> GetOutgoingAsync(RentalQuery? query = null, CancellationToken ct = default)
    {
        var currentUserId = RequireCurrentUserId();

        using var db = _factory.CreateDbContext();
        var q = db.Rentals
            .Include(r => r.Item!)
                .ThenInclude(i => i.Owner)
            .Include(r => r.Item!)
                .ThenInclude(i => i.Category)
            .Include(r => r.Borrower)
            .Where(r => r.BorrowerId == currentUserId)
            .AsQueryable();

        q = ApplyStatusFilter(q, query?.Status);

        var rentals = await q
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        foreach (var r in rentals)
        {
            PopulateDisplayFields(r);
            r.Status = RentalStatusHelper.DeriveStatus(r.Status, r.EndDate);
        }

        return rentals;
    }

    // ---- Writes -----------------------------------------------------------

    public async Task<Rental> RequestAsync(int itemId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var currentUserId = RequireCurrentUserId();

        using var db = _factory.CreateDbContext();

        var item = await db.Items
            .Include(i => i.Owner)
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new InvalidOperationException($"Item {itemId} not found.");

        var durationDays = endDate.DayNumber - startDate.DayNumber;
        if (durationDays <= 0)
            throw new ArgumentException("EndDate must be after StartDate.");

        var rental = new Rental
        {
            ItemId     = itemId,
            BorrowerId = currentUserId,
            StartDate  = startDate,
            EndDate    = endDate,
            Status     = RentalStatus.Requested,
            TotalPrice = item.DailyRate * durationDays,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };

        db.Rentals.Add(rental);
        await db.SaveChangesAsync(ct);

        // Attach the loaded item so PopulateDisplayFields has the navigation data.
        rental.Item = item;
        PopulateDisplayFields(rental);
        return rental;
    }

    public async Task<RentalStatusUpdate> UpdateStatusAsync(
        int rentalId, RentalStatus newStatus, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();

        var rental = await db.Rentals.FindAsync(new object[] { rentalId }, ct)
            ?? throw new InvalidOperationException($"Rental {rentalId} not found.");

        // Overdue is client-side only — persist the underlying OutForRent value.
        var dbStatus = newStatus == RentalStatus.Overdue
            ? RentalStatus.OutForRent
            : newStatus;

        rental.Status    = dbStatus;
        rental.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Re-derive so the caller gets Overdue back if the date has already passed.
        var displayStatus = RentalStatusHelper.DeriveStatus(dbStatus, rental.EndDate);
        return new RentalStatusUpdate(rental.Id, displayStatus, rental.UpdatedAt);
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// Applies an optional status filter. When the query asks for
    /// <see cref="RentalStatus.Overdue"/> we match <c>OutForRent</c> rows
    /// whose EndDate is before today (since Overdue is never stored).
    /// </summary>
    private static IQueryable<Rental> ApplyStatusFilter(IQueryable<Rental> q, RentalStatus? status)
    {
        if (status is null) return q;

        if (status == RentalStatus.Overdue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return q.Where(r => r.Status == RentalStatus.OutForRent && r.EndDate < today);
        }

        return q.Where(r => r.Status == status);
    }

    private static void PopulateDisplayFields(Rental rental)
    {
        if (rental.Item is not null)
        {
            rental.ItemTitle    = rental.Item.Title;
            rental.ItemDailyRate = rental.Item.DailyRate;

            if (rental.Item.Owner is not null)
            {
                rental.OwnerId   = rental.Item.Owner.Id;
                rental.OwnerName = FormatDisplayName(
                    rental.Item.Owner.FirstName, rental.Item.Owner.LastName);
            }
        }

        if (rental.Borrower is not null)
            rental.BorrowerName = FormatDisplayName(
                rental.Borrower.FirstName, rental.Borrower.LastName);
    }

    private static string FormatDisplayName(string firstName, string lastName) =>
        string.IsNullOrEmpty(lastName) ? firstName : $"{firstName} {lastName[0]}.";

    private int RequireCurrentUserId() =>
        _currentUser.CurrentUserId
        ?? throw new InvalidOperationException("No authenticated user — cannot perform this operation.");
}
