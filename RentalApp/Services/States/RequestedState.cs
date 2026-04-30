using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// A rental that has been submitted but not yet acted on by the owner.
/// The owner may approve or reject it; no other transitions are valid.
/// </summary>
public sealed class RequestedState : RentalState
{
    public override RentalStatus Status => RentalStatus.Requested;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>
        {
            RentalStatus.Approved,
            RentalStatus.Rejected,
        };
}
