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
        // Arrange: a rental spanning 5 days.
        var rental = new Rental
        {
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 6),
        };

        // Act + Assert: DurationDays is calculated from the date range.
        Assert.Equal(5, rental.DurationDays);
    }

    [Fact]
    public void DurationDays_IsZero_WhenStartEqualsEnd()
    {
        // Arrange: start and end are the same date.
        var date = new DateOnly(2026, 5, 1);
        var rental = new Rental { StartDate = date, EndDate = date };

        // Act + Assert: no duration when dates match.
        Assert.Equal(0, rental.DurationDays);
    }

    [Fact]
    public void Status_DefaultsToRequested()
    {
        // Arrange: a new rental.
        var rental = new Rental();

        // Act + Assert: status should default to Requested.
        Assert.Equal(RentalStatus.Requested, rental.Status);
    }

    [Fact]
    public void IsActiveOnToday_IsTrue_WhenTodayWithinRange()
    {
        // Arrange: a rental active today (inclusive range).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rental = new Rental { StartDate = today, EndDate = today };

        // Act + Assert: IsActiveOnToday is true when today is within the range.
        Assert.True(rental.IsActiveOnToday);
    }
}
