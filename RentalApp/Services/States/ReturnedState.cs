using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// The item has been physically returned by the borrower. The owner
/// inspects it and marks the rental Completed to close it out.
/// </summary>
public sealed class ReturnedState : RentalState
{
    public override RentalStatus Status => RentalStatus.Returned;

    public override IReadOnlySet<RentalStatus> ValidTransitions { get; } =
        new HashSet<RentalStatus>
        {
            RentalStatus.Completed,
        };
}
