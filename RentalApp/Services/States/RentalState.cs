/// @file RentalState.cs
/// @brief Abstract base class for the rental state machine (GoF State Pattern)
/// @author RentalApp Development Team
/// @date 2026

using RentalApp.Database.Models;

namespace RentalApp.Services.States;

/// <summary>
/// Abstract base for all rental lifecycle states. Each concrete subclass
/// declares exactly which transitions it permits via
/// <see cref="ValidTransitions"/>; all others are blocked at this layer
/// (and again server-side via 409).
///
/// Usage:
/// <code>
///   var state = RentalState.For(rental.Status);
///   if (state.CanTransitionTo(RentalStatus.Approved))
///       state.TransitionTo(RentalStatus.Approved); // returns ApprovedState
/// </code>
/// </summary>
public abstract class RentalState
{
    /// <summary>The <see cref="RentalStatus"/> this state represents.</summary>
    public abstract RentalStatus Status { get; }

    /// <summary>
    /// Set of <see cref="RentalStatus"/> values that are legal targets from
    /// this state. Concrete states initialise this once and it is read-only.
    /// </summary>
    public abstract IReadOnlySet<RentalStatus> ValidTransitions { get; }

    /// <summary>Returns true if transitioning to <paramref name="target"/> is permitted.</summary>
    public bool CanTransitionTo(RentalStatus target) =>
        ValidTransitions.Contains(target);

    /// <summary>
    /// Validates and performs the transition to <paramref name="target"/>.
    /// </summary>
    /// <returns>The <see cref="RentalState"/> for <paramref name="target"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="target"/> is not in <see cref="ValidTransitions"/>.
    /// </exception>
    public RentalState TransitionTo(RentalStatus target)
    {
        if (!CanTransitionTo(target))
        {
            throw new InvalidOperationException(
                $"Cannot transition rental from {Status} to {target}.");
        }
        return For(target);
    }

    /// <summary>
    /// Factory: returns the <see cref="RentalState"/> instance for the given
    /// <paramref name="status"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown for any unknown <see cref="RentalStatus"/> value.
    /// </exception>
    public static RentalState For(RentalStatus status) => status switch
    {
        RentalStatus.Requested  => new RequestedState(),
        RentalStatus.Approved   => new ApprovedState(),
        RentalStatus.OutForRent => new OutForRentState(),
        RentalStatus.Overdue    => new OverdueState(),
        RentalStatus.Returned   => new ReturnedState(),
        RentalStatus.Completed  => new CompletedState(),
        RentalStatus.Rejected   => new RejectedState(),
        _ => throw new ArgumentOutOfRangeException(
                 nameof(status), status, "Unknown rental status."),
    };
}
