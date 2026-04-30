using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// The owner has accepted the request. The item can now be handed over,
/// moving the rental to OutForRent.
/// </summary>
public sealed class ApprovedState : RentalState
{
    public override RentalStatus Status => RentalStatus.Approved;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>
        {
            RentalStatus.OutForRent,
        };
}
