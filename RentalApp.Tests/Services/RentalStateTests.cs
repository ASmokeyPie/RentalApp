using RentalApp.Database.Models;
using RentalApp.Services.States;

namespace RentalApp.Tests.Services;

/// <summary>
/// Tests for the State Pattern implementation. Verifies that each concrete
/// state class permits exactly the right transitions and blocks all others.
/// </summary>
public class RentalStateTests
{
    // ---- Factory ----------------------------------------------------------

    [Theory]
    [InlineData(RentalStatus.Requested, typeof(RequestedState))]
    [InlineData(RentalStatus.Approved, typeof(ApprovedState))]
    [InlineData(RentalStatus.OutForRent, typeof(OutForRentState))]
    [InlineData(RentalStatus.Overdue, typeof(OverdueState))]
    [InlineData(RentalStatus.Returned, typeof(ReturnedState))]
    [InlineData(RentalStatus.Completed, typeof(CompletedState))]
    [InlineData(RentalStatus.Rejected, typeof(RejectedState))]
    public void For_ReturnsCorrectConcreteType(RentalStatus status, Type expectedType)
    {
        // Arrange + Act: build the state via the factory.
        var state = RentalState.For(status);

        // Assert: returned type matches the status.
        Assert.IsType(expectedType, state);
    }

    [Fact]
    public void For_SetsStatusProperty()
    {
        // Arrange + Act + Assert: factory sets Status consistently for all enum values.
        foreach (var status in Enum.GetValues<RentalStatus>())
        {
            Assert.Equal(status, RentalState.For(status).Status);
        }
    }

    // ---- RequestedState ---------------------------------------------------

    [Theory]
    [InlineData(RentalStatus.Approved, true)]
    [InlineData(RentalStatus.Rejected, true)]
    [InlineData(RentalStatus.OutForRent, false)]
    [InlineData(RentalStatus.Overdue, false)]
    [InlineData(RentalStatus.Returned, false)]
    [InlineData(RentalStatus.Completed, false)]
    [InlineData(RentalStatus.Requested, false)]
    public void RequestedState_CanTransitionTo(RentalStatus target, bool expected)
    {
        // Arrange + Act + Assert
        Assert.Equal(expected, new RequestedState().CanTransitionTo(target));
    }

    // ---- ApprovedState ----------------------------------------------------

    [Theory]
    [InlineData(RentalStatus.OutForRent, true)]
    [InlineData(RentalStatus.Approved, false)]
    [InlineData(RentalStatus.Rejected, false)]
    [InlineData(RentalStatus.Returned, false)]
    [InlineData(RentalStatus.Completed, false)]
    public void ApprovedState_CanTransitionTo(RentalStatus target, bool expected)
    {
        // Arrange + Act + Assert
        Assert.Equal(expected, new ApprovedState().CanTransitionTo(target));
    }

    // ---- OutForRentState --------------------------------------------------

    [Theory]
    [InlineData(RentalStatus.Returned, true)]
    [InlineData(RentalStatus.OutForRent, false)]
    [InlineData(RentalStatus.Completed, false)]
    [InlineData(RentalStatus.Approved, false)]
    public void OutForRentState_CanTransitionTo(RentalStatus target, bool expected)
    {
        // Arrange + Act + Assert
        Assert.Equal(expected, new OutForRentState().CanTransitionTo(target));
    }

    // ---- OverdueState -----------------------------------------------------

    [Theory]
    [InlineData(RentalStatus.Returned, true)]
    [InlineData(RentalStatus.OutForRent, false)]
    [InlineData(RentalStatus.Completed, false)]
    [InlineData(RentalStatus.Approved, false)]
    [InlineData(RentalStatus.Overdue, false)]
    public void OverdueState_CanTransitionTo(RentalStatus target, bool expected)
    {
        // Arrange + Act + Assert
        Assert.Equal(expected, new OverdueState().CanTransitionTo(target));
    }

    // ---- ReturnedState ----------------------------------------------------

    [Theory]
    [InlineData(RentalStatus.Completed, true)]
    [InlineData(RentalStatus.Returned, false)]
    [InlineData(RentalStatus.OutForRent, false)]
    [InlineData(RentalStatus.Approved, false)]
    public void ReturnedState_CanTransitionTo(RentalStatus target, bool expected)
    {
        // Arrange + Act + Assert
        Assert.Equal(expected, new ReturnedState().CanTransitionTo(target));
    }

    // ---- Terminal states: no outgoing transitions -------------------------

    [Theory]
    [InlineData(RentalStatus.Requested)]
    [InlineData(RentalStatus.Approved)]
    [InlineData(RentalStatus.OutForRent)]
    [InlineData(RentalStatus.Returned)]
    [InlineData(RentalStatus.Completed)]
    [InlineData(RentalStatus.Rejected)]
    public void CompletedState_CannotTransitionToAnything(RentalStatus target)
    {
        // Arrange + Act + Assert
        Assert.False(new CompletedState().CanTransitionTo(target));
    }

    [Theory]
    [InlineData(RentalStatus.Requested)]
    [InlineData(RentalStatus.Approved)]
    [InlineData(RentalStatus.OutForRent)]
    [InlineData(RentalStatus.Returned)]
    [InlineData(RentalStatus.Completed)]
    [InlineData(RentalStatus.Rejected)]
    public void RejectedState_CannotTransitionToAnything(RentalStatus target)
    {
        // Arrange + Act + Assert
        Assert.False(new RejectedState().CanTransitionTo(target));
    }

    // ---- TransitionTo -----------------------------------------------------

    [Fact]
    public void TransitionTo_ReturnsCorrectNextState_OnLegalTransition()
    {
        // Arrange + Act
        var next = new RequestedState().TransitionTo(RentalStatus.Approved);

        // Assert
        Assert.IsType<ApprovedState>(next);
        Assert.Equal(RentalStatus.Approved, next.Status);
    }

    [Fact]
    public void TransitionTo_Throws_OnIllegalTransition()
    {
        // Arrange + Act
        var ex = Assert.Throws<InvalidOperationException>(
            () => new RequestedState().TransitionTo(RentalStatus.Completed));

        // Assert: error message includes both states.
        Assert.Contains("Requested", ex.Message);
        Assert.Contains("Completed", ex.Message);
    }

    [Fact]
    public void TransitionTo_OverdueToReturned_ReturnsReturnedState()
    {
        // Arrange + Act
        var next = new OverdueState().TransitionTo(RentalStatus.Returned);

        // Assert
        Assert.IsType<ReturnedState>(next);
    }

    [Fact]
    public void TransitionTo_CompletedState_AlwaysThrows()
    {
        // Arrange + Act + Assert
        Assert.Throws<InvalidOperationException>(
            () => new CompletedState().TransitionTo(RentalStatus.Requested));
    }
}
