using RentalApp.Database.Models;

namespace RentalApp.Tests.Models;

/// <summary>
/// Performs rental duration calculation tests and verifies that the RentalStatus enum is present and defaults to Requested.
/// </summary>
public class RentalTests
{
    [Fact]
    public void DurationDays_IsEndDateMinusStartDate()
    {
        var rental = new Rental
        {
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 6),
        };

        Assert.Equal(5, rental.DurationDays);
    }

    [Fact]
    public void DurationDays_IsZero_WhenStartEqualsEnd()
    {
        var date = new DateOnly(2026, 5, 1);
        var rental = new Rental { StartDate = date, EndDate = date };

        Assert.Equal(0, rental.DurationDays);
    }

    [Fact]
    public void Status_DefaultsToRequested()
    {
        var rental = new Rental();
        Assert.Equal(RentalStatus.Requested, rental.Status);
    }

    [Fact]
    public void IsActiveOnToday_IsTrue_WhenTodayWithinRange()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rental = new Rental { StartDate = today, EndDate = today };

        Assert.True(rental.IsActiveOnToday);
    }
}
