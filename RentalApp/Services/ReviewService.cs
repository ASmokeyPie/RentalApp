using RentalApp.Database.Models;
using RentalApp.Database.Repositories;

namespace RentalApp.Services;

/// <summary>
/// Default <see cref="IReviewService"/> implementation. Validates the rules
/// the requirements + plan call out, then delegates to
/// <see cref="IReviewRepository.CreateAsync(int, int, string?, CancellationToken)"/>.
/// MAUI-free so the .NET 10 test project can exercise it directly.
/// </summary>
public sealed class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviews;

    public ReviewService(IReviewRepository reviews) => _reviews = reviews;

    public Task<Review> SubmitReviewAsync(
        Rental rental,
        int rating,
        string? comment,
        int currentUserId,
        CancellationToken ct = default)
    {
        if (rental is null)
        {
            throw new ArgumentNullException(nameof(rental));
        }
        if (currentUserId == 0)
        {
            throw new InvalidOperationException("You must be signed in to leave a review.");
        }
        if (rental.Status != RentalStatus.Completed)
        {
            throw new InvalidOperationException(
                "You can only review a rental once it has been marked Completed.");
        }
        if (rental.BorrowerId != currentUserId)
        {
            throw new InvalidOperationException("Only the borrower can review a rental.");
        }
        if (rating < 1 || rating > 5)
        {
            throw new InvalidOperationException("Rating must be between 1 and 5 stars.");
        }
        if (comment is { Length: > 500 })
        {
            throw new InvalidOperationException("Comment must be 500 characters or fewer.");
        }

        // Server enforces the "one review per rental" rule and will return 409
        // (surfaced via the repository's EnsureSuccessOrThrowApiErrorAsync
        // helper) if the borrower already reviewed this rental.
        return _reviews.CreateAsync(rental.Id, rating, comment, ct);
    }

    public bool IsRentalReviewable(Rental rental, int currentUserId) =>
        rental is not null
        && currentUserId != 0
        && rental.Status == RentalStatus.Completed
        && rental.BorrowerId == currentUserId;
}
