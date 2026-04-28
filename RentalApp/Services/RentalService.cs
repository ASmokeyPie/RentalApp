using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;

namespace RentalApp.Services;

/// <summary>
/// Default <see cref="IRentalService"/> implementation. Wraps an
/// <see cref="IRentalRepository"/> for persistence and adds the rental-
/// specific business rules. MAUI-free so it can be exercised by the .NET 10
/// test project without any UI dependencies.
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
        if (endDate < startDate)
        {
            throw new InvalidOperationException("End date must be on or after start date.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (startDate < today)
        {
            throw new InvalidOperationException("Start date cannot be in the past.");
        }

        return _rentals.RequestAsync(itemId, startDate, endDate, ct);
    }

    public Task<RentalStatusUpdate> TransitionAsync(
        int rentalId,
        RentalStatus currentStatus,
        RentalStatus targetStatus,
        CancellationToken ct = default)
    {
        if (!IsTransitionLegal(currentStatus, targetStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition rental {rentalId} from {currentStatus} to {targetStatus}.");
        }

        return _rentals.UpdateStatusAsync(rentalId, targetStatus, ct);
    }

    public Task<RentalStatusUpdate> ApproveAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default) =>
        TransitionAsync(rentalId, currentStatus, RentalStatus.Approved, ct);

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
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        // Inclusive day count: startDate and endDate both count.
        var days = endDate.DayNumber - startDate.DayNumber + 1;
        return dailyRate * days;
    }

    public bool IsTransitionLegal(RentalStatus from, RentalStatus to) =>
        LegalTransitions.Contains((from, to));

    public bool HasOverlap(IEnumerable<Rental> existing, DateOnly startDate, DateOnly endDate)
    {
        if (existing is null) return false;

        foreach (var r in existing)
        {
            if (IsTerminal(r.Status)) continue;
            // Inclusive ranges overlap iff start ≤ otherEnd AND otherStart ≤ end.
            if (startDate <= r.EndDate && r.StartDate <= endDate)
            {
                return true;
            }
        }
        return false;
    }

    // ---- State machine ----------------------------------------------------

    /// <summary>
    /// Canonical state-machine transition table. Mirrored on the server; the
    /// client check is for UX (so we don't show illegal action buttons or send
    /// requests we know will 409).
    /// </summary>
    private static readonly HashSet<(RentalStatus From, RentalStatus To)> LegalTransitions = new()
    {
        // From Requested
        (RentalStatus.Requested, RentalStatus.Approved),
        (RentalStatus.Requested, RentalStatus.Rejected),

        // From Approved
        (RentalStatus.Approved,  RentalStatus.OutForRent),

        // From OutForRent
        (RentalStatus.OutForRent, RentalStatus.Returned),

        // From Returned
        (RentalStatus.Returned, RentalStatus.Completed),

        // Rejected, Completed are terminal — no outgoing edges.
    };

    private static bool IsTerminal(RentalStatus s) =>
        s is RentalStatus.Rejected or RentalStatus.Completed;
}
