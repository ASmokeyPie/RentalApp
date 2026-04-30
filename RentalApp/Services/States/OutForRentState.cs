using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// The item has been handed over to the borrower and the rental is active.
/// The borrower marks it Returned when they bring the item back.
/// </summary>
public sealed class OutForRentState : RentalState
{
    public override RentalStatus Status => RentalStatus.OutForRent;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>
        {
            RentalStatus.Returned,
        };
}
