using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// Terminal state. The owner declined the rental request.
/// No further transitions are permitted.
/// </summary>
public sealed class RejectedState : RentalState
{
    public override RentalStatus Status => RentalStatus.Rejected;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>();
}
