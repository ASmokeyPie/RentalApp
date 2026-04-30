using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// Terminal state. The rental lifecycle is fully closed.
/// No further transitions are permitted.
/// </summary>
public sealed class CompletedState : RentalState
{
    public override RentalStatus Status => RentalStatus.Completed;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>();
}
