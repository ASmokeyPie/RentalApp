using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;
using RentalApp.Services.States;

namespace RentalApp.Services;

/// <summary>
/// Default <see cref="IRentalService"/> implementation. Wraps an
/// <see cref="IRentalRepository"/> for persistence and adds the rental-
/// specific business rules.
/// </summary>
public sealed class RentalService : IRentalService
{
    private readonly IRentalRepository _rentals;

    public RentalService(IRentalRepository rentals) => _rentals = rentals;

    // ---- Reads ------------------------------------------------------------

    public Task<Rental?> GetRentalAsync(int rentalId, CancellationToken ct = default) =>
        _rentals.GetByIdAsync(rentalId, ct);

    public Task<IReadOnlyList<Rental>> GetIncomingAsync(RentalQuery? query = null, CancellationToken ct = default) =>
        _rentals.GetIncomingAsync(query, ct);

    public Task<IReadOnlyList<Rental>> GetOutgoingAsync(RentalQuery? query = null, CancellationToken ct = default) =>
        _rentals.GetOutgoingAsync(query, ct);

    // ---- Writes -----------------------------------------------------------

    public Task<Rental> RequestRentalAsync(int itemId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        // Basic date validation (server will still validate availability).
        if (endDate < startDate)
        {
            throw new InvalidOperationException("End date must be on or after start date.");
        }

        // Prevent creating requests that start before today (UTC).
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (startDate < today)
        {
            throw new InvalidOperationException("Start date cannot be in the past.");
        }

        // Delegate persistence / API call to the repository.
        return _rentals.RequestAsync(itemId, startDate, endDate, ct);
    }

    public Task<RentalStatusUpdate> TransitionAsync(
        int rentalId,
        RentalStatus currentStatus,
        RentalStatus targetStatus,
        CancellationToken ct = default)
    {
        // Client-side guard: ask the current state whether the target is legal.
        // Each RentalState subclass declares its own ValidTransitions; invalid
        // attempts throw here before any network call is made.
        // (The server mirrors this and remains the authoritative source of truth.)
        RentalState.For(currentStatus).TransitionTo(targetStatus); // throws if illegal

        // For Overdue → Returned, the server still sees OutForRent → Returned,
        // so send Returned (not Overdue) as the target.
        return _rentals.UpdateStatusAsync(rentalId, targetStatus, ct);
    }

    public async Task<RentalStatusUpdate> ApproveAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default)
    {
        // Fetch the rental so we know which item and date range we're approving.
        var rental = await _rentals.GetByIdAsync(rentalId, ct)
            ?? throw new InvalidOperationException($"Rental {rentalId} not found.");

        // Check all other incoming rentals for this item. Only Approved, OutForRent,
        // and Overdue statuses are "committed" and block a new approval — pending
        // Requested rentals are still competing and do not block each other.
        var incoming = await _rentals.GetIncomingAsync(null, ct);
        var activeConflicts = incoming.Where(r =>
            r.ItemId == rental.ItemId &&
            r.Id     != rentalId &&
            r.Status is RentalStatus.Approved or RentalStatus.OutForRent or RentalStatus.Overdue);

        if (HasOverlap(activeConflicts, rental.StartDate, rental.EndDate))
            throw new InvalidOperationException(
                "This item is already committed for the requested dates and cannot be approved for another rental.");

        return await TransitionAsync(rentalId, currentStatus, RentalStatus.Approved, ct);
    }

    public Task<RentalStatusUpdate> RejectAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default) =>
        TransitionAsync(rentalId, currentStatus, RentalStatus.Rejected, ct);

    public Task<RentalStatusUpdate> MarkOutForRentAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default) =>
        TransitionAsync(rentalId, currentStatus, RentalStatus.OutForRent, ct);

    public Task<RentalStatusUpdate> MarkReturnedAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default) =>
        TransitionAsync(rentalId, currentStatus, RentalStatus.Returned, ct);

    public Task<RentalStatusUpdate> MarkCompletedAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default) =>
        TransitionAsync(rentalId, currentStatus, RentalStatus.Completed, ct);

    // ---- Pure helpers -----------------------------------------------------

    public decimal CalculatePrice(decimal dailyRate, DateOnly startDate, DateOnly endDate)
    {
        // Pricing assumes a non-negative duration.
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        // Inclusive day count: startDate and endDate both count.
        var days = endDate.DayNumber - startDate.DayNumber + 1;

        // Total price is a straight daily-rate * day-count calculation.
        return dailyRate * days;
    }

    /// <summary>
    /// Delegates to <see cref="RentalState.CanTransitionTo"/> so callers can
    /// check legality without catching exceptions. The state objects are the
    /// single source of truth for the transition table.
    /// </summary>
    public bool IsTransitionLegal(RentalStatus from, RentalStatus to) =>
        RentalState.For(from).CanTransitionTo(to);

    public bool HasOverlap(IEnumerable<Rental> existing, DateOnly startDate, DateOnly endDate)
    {
        // Null means "no existing rentals to check against".
        if (existing is null) return false;

        foreach (var r in existing)
        {
            // Terminal rentals no longer block availability.
            // Overdue is non-terminal — the item is still out so it blocks.
            if (IsTerminal(r.Status)) continue;

            // Inclusive ranges overlap iff start ≤ otherEnd AND otherStart ≤ end.
            if (startDate <= r.EndDate && r.StartDate <= endDate)
            {
                return true;
            }
        }
        return false;
    }

    // ---- Helpers ----------------------------------------------------------

    private static bool IsTerminal(RentalStatus s) =>
        s is RentalStatus.Rejected or RentalStatus.Completed;
}
