using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Services;

/// <summary>
/// Application service for the rental domain. Wraps
/// <see cref="Database.Repositories.IRentalRepository"/> and adds the rental-
/// specific business rules:
/// <list type="bullet">
///   <item><description>State-machine validation (legal transitions only).</description></item>
///   <item><description>Date-range validation (start ≤ end, no past starts).</description></item>
///   <item><description>Pure helpers for price calculation and overlap detection
///     so callers don't replicate the math.</description></item>
/// </list>
/// All rental-related ViewModels depend on this interface rather than the
/// repository directly, so the data-source choice (API vs local DB) stays
/// invisible to the UI and the business rules are the single source of truth.
/// </summary>
public interface IRentalService
{
    // ---- Reads ------------------------------------------------------------

    /// <summary>Fetch a single rental by id (or null if not found / not visible to current user).</summary>
    Task<Rental?> GetRentalAsync(int rentalId, CancellationToken ct = default);

    /// <summary>Rentals where the current user is the owner.</summary>
    Task<IReadOnlyList<Rental>> GetIncomingAsync(RentalQuery? query = null, CancellationToken ct = default);

    /// <summary>Rentals where the current user is the borrower.</summary>
    Task<IReadOnlyList<Rental>> GetOutgoingAsync(RentalQuery? query = null, CancellationToken ct = default);

    // ---- Writes -----------------------------------------------------------

    /// <summary>
    /// Submit a new rental request. Performs client-side validation
    /// (start ≤ end, start ≥ today). The server is the source of truth for
    /// availability and double-booking and may still return 409 — which the
    /// repository surfaces as <see cref="HttpRequestException"/>.
    /// </summary>
    Task<Rental> RequestRentalAsync(int itemId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Move a rental from <paramref name="currentStatus"/> to
    /// <paramref name="targetStatus"/>. Throws
    /// <see cref="InvalidOperationException"/> if the transition isn't legal.
    /// Otherwise delegates to PATCH /rentals/{id}/status.
    /// </summary>
    Task<RentalStatusUpdate> TransitionAsync(int rentalId, RentalStatus currentStatus, RentalStatus targetStatus, CancellationToken ct = default);

    // Convenience wrappers around TransitionAsync — same rules, named call sites.
    Task<RentalStatusUpdate> ApproveAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default);
    Task<RentalStatusUpdate> RejectAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default);
    Task<RentalStatusUpdate> MarkOutForRentAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default);
    Task<RentalStatusUpdate> MarkReturnedAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default);
    Task<RentalStatusUpdate> MarkCompletedAsync(int rentalId, RentalStatus currentStatus, CancellationToken ct = default);

    // ---- Pure helpers -----------------------------------------------------
    // These take no service state — they're on the interface so consumers can
    // mock them in tests if needed and so the rules sit alongside the rest of
    // the rental contract.

    /// <summary>
    /// Total price for a rental at <paramref name="dailyRate"/> covering the
    /// inclusive range <paramref name="startDate"/>..<paramref name="endDate"/>.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="endDate"/> is before <paramref name="startDate"/>.</exception>
    decimal CalculatePrice(decimal dailyRate, DateOnly startDate, DateOnly endDate);

    /// <summary>True if the requested transition is permitted by the state machine.</summary>
    bool IsTransitionLegal(RentalStatus from, RentalStatus to);

    /// <summary>
    /// True if any non-terminal rental in <paramref name="existing"/> overlaps
    /// the inclusive range <paramref name="startDate"/>..<paramref name="endDate"/>.
    /// Terminal states (Rejected / Completed) are skipped.
    /// </summary>
    bool HasOverlap(IEnumerable<Rental> existing, DateOnly startDate, DateOnly endDate);
}
