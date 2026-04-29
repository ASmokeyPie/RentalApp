using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Application service for the review domain. Wraps
/// <see cref="Database.Repositories.IReviewRepository"/> and adds the
/// review-specific business rules:
/// <list type="bullet">
///   <item><description>A review can only be left after the rental is
///     <see cref="RentalStatus.Completed"/>.</description></item>
///   <item><description>Only the borrower of a rental may review it
///     (the owner doesn't review their own item).</description></item>
///   <item><description>Rating must be 1–5.</description></item>
///   <item><description>The "no duplicate review per rental" rule is enforced
///     server-side and surfaces as an HTTP 409 — the message comes back via
///     the repo's error-handling helper.</description></item>
/// </list>
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Submits a review for a completed rental.
    /// </summary>
    /// <param name="rental">The rental being reviewed (already loaded by the
    ///     caller). Used to look up status, rentalId, and borrowerId for the
    ///     authorisation check.</param>
    /// <param name="rating">Star rating, 1–5.</param>
    /// <param name="comment">Optional free-text comment (≤500 chars).</param>
    /// <param name="currentUserId">The id of the authenticated user; must
    ///     match <see cref="Rental.BorrowerId"/>.</param>
    /// <returns>The created <see cref="Review"/> as returned by the API.</returns>
    /// <exception cref="InvalidOperationException">When validation fails
    ///     (rental not completed, viewer not borrower, rating out of range).</exception>
    Task<Review> SubmitReviewAsync(
        Rental rental,
        int rating,
        string? comment,
        int currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// True if the given rental is in a state where the given user is allowed
    /// to leave a review (Completed AND user is the borrower). The
    /// "no existing review yet" check is server-side; this client-side helper
    /// is best-effort UX gating.
    /// </summary>
    bool IsRentalReviewable(Rental rental, int currentUserId);
}
