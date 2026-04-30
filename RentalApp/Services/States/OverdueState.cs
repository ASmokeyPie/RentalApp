using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// Client-side derived state: an OutForRent rental whose EndDate has passed.
/// The server has no concept of Overdue — it still considers the rental
/// OutForRent. The only valid action is for the borrower to return the item,
/// transitioning to Returned (which the API accepts as "Out for Rent" → Returned).
/// </summary>
public sealed class OverdueState : RentalState
{
    public override RentalStatus Status => RentalStatus.Overdue;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>
        {
            RentalStatus.Returned,
        };
}
