using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.Tests.Services;

public class RentalServiceTests
{
    // ---- CalculatePrice ---------------------------------------------------

    [Fact]
    public void CalculatePrice_SameDay_ChargesOneDay()
    {
        // Arrange
        var svc = Build();

        // Act
        var price = svc.CalculatePrice(10m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 1));

        // Assert
        Assert.Equal(10m, price);
    }

    [Fact]
    public void CalculatePrice_MultiDay_ChargesInclusiveDays()
    {
        // 1st through 3rd inclusive = 3 days at £5 = £15
        // Arrange
        var svc = Build();

        // Act
        var price = svc.CalculatePrice(5m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));

        // Assert
        Assert.Equal(15m, price);
    }

    [Fact]
    public void CalculatePrice_ZeroRate_ProducesZero()
    {
        // Arrange
        var svc = Build();

        // Act
        var price = svc.CalculatePrice(0m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));

        // Assert
        Assert.Equal(0m, price);
    }

    [Fact]
    public void CalculatePrice_EndBeforeStart_Throws()
    {
        // Arrange
        var svc = Build();

        // Act + Assert
        Assert.Throws<ArgumentException>(
            () => svc.CalculatePrice(5m, new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 1)));
    }

    // ---- IsTransitionLegal ------------------------------------------------

    [Theory]
    // From Requested
    [InlineData(RentalStatus.Requested, RentalStatus.Approved,    true)]
    [InlineData(RentalStatus.Requested, RentalStatus.Rejected,    true)]
    [InlineData(RentalStatus.Requested, RentalStatus.OutForRent,  false)]
    [InlineData(RentalStatus.Requested, RentalStatus.Returned,    false)]
    [InlineData(RentalStatus.Requested, RentalStatus.Completed,   false)]
    // From Approved
    [InlineData(RentalStatus.Approved,  RentalStatus.OutForRent,  true)]
    [InlineData(RentalStatus.Approved,  RentalStatus.Rejected,    false)]
    [InlineData(RentalStatus.Approved,  RentalStatus.Completed,   false)]
    // From OutForRent
    [InlineData(RentalStatus.OutForRent, RentalStatus.Returned,   true)]
    [InlineData(RentalStatus.OutForRent, RentalStatus.Completed,  false)]
    // From Overdue (client-side derived: OutForRent past end date)
    [InlineData(RentalStatus.Overdue, RentalStatus.Returned,      true)]
    [InlineData(RentalStatus.Overdue, RentalStatus.Completed,     false)]
    [InlineData(RentalStatus.Overdue, RentalStatus.Approved,      false)]
    // From Returned
    [InlineData(RentalStatus.Returned, RentalStatus.Completed,    true)]
    [InlineData(RentalStatus.Returned, RentalStatus.OutForRent,   false)]
    // Terminal states have no outgoing edges
    [InlineData(RentalStatus.Rejected,  RentalStatus.Approved,    false)]
    [InlineData(RentalStatus.Completed, RentalStatus.Approved,    false)]
    // Self-transition not legal
    [InlineData(RentalStatus.Requested, RentalStatus.Requested,   false)]
    public void IsTransitionLegal_RespectsStateMachine(RentalStatus from, RentalStatus to, bool expected)
    {
        // Arrange
        var svc = Build();

        // Act + Assert
        Assert.Equal(expected, svc.IsTransitionLegal(from, to));
    }

    // ---- HasOverlap -------------------------------------------------------

    [Fact]
    public void HasOverlap_NoOverlap_WhenRangesAreDisjoint()
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), RentalStatus.Approved) };

        // Act + Assert
        Assert.False(svc.HasOverlap(existing, new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 6)));
    }

    [Fact]
    public void HasOverlap_OverlapsOnExactMatch()
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), RentalStatus.Approved) };

        // Act + Assert
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3)));
    }

    [Fact]
    public void HasOverlap_OverlapsOnPartialLeft()
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 10), RentalStatus.Approved) };

        // Act + Assert
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 3), new DateOnly(2026, 5, 6)));
    }

    [Fact]
    public void HasOverlap_OverlapsOnPartialRight()
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 10), RentalStatus.Approved) };

        // Act + Assert
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 9), new DateOnly(2026, 5, 12)));
    }

    [Fact]
    public void HasOverlap_OverlapsWhenContained()
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30), RentalStatus.Approved) };

        // Act + Assert
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12)));
    }

    [Fact]
    public void HasOverlap_OverlapsWhenContaining()
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12), RentalStatus.Approved) };

        // Act + Assert
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30)));
    }

    [Fact]
    public void HasOverlap_OverdueRentalBlocksAvailability()
    {
        // Overdue is non-terminal — item is still out, so it must block.
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30), RentalStatus.Overdue) };

        // Act + Assert
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12)));
    }

    [Theory]
    [InlineData(RentalStatus.Rejected)]
    [InlineData(RentalStatus.Completed)]
    public void HasOverlap_IgnoresTerminalStateRentals(RentalStatus terminalStatus)
    {
        // Arrange
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30), terminalStatus) };

        // Act + Assert
        Assert.False(svc.HasOverlap(existing, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12)));
    }

    [Fact]
    public void HasOverlap_HandlesNullCollection()
    {
        // Arrange
        var svc = Build();

        // Act + Assert
        Assert.False(svc.HasOverlap(null!, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3)));
    }

    // ---- RequestRentalAsync ----------------------------------------------

    [Fact]
    public async Task RequestRentalAsync_DelegatesToRepo_OnHappyPath()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(7);
        var ret = future.AddDays(2);
        repo.Setup(r => r.RequestAsync(42, future, ret, default))
            .ReturnsAsync(SampleRental(99, RentalStatus.Requested));

        // Act
        var rental = await svc.RequestRentalAsync(42, future, ret);

        // Assert
        Assert.Equal(99, rental.Id);
        repo.Verify(r => r.RequestAsync(42, future, ret, default), Times.Once);
    }

    [Fact]
    public async Task RequestRentalAsync_Throws_WhenEndBeforeStart()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RequestRentalAsync(42, new DateOnly(2027, 5, 5), new DateOnly(2027, 5, 1)));

        // Assert
        Assert.Contains("End date", ex.Message);
        repo.Verify(r => r.RequestAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default), Times.Never);
    }

    [Fact]
    public async Task RequestRentalAsync_Throws_WhenStartInPast()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RequestRentalAsync(42, past, past.AddDays(2)));

        // Assert
        Assert.Contains("past", ex.Message, StringComparison.OrdinalIgnoreCase);
        repo.Verify(r => r.RequestAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default), Times.Never);
    }

    // ---- TransitionAsync --------------------------------------------------

    [Fact]
    public async Task TransitionAsync_DelegatesToRepo_OnLegalTransition()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));

        // Act
        var result = await svc.TransitionAsync(7, RentalStatus.Requested, RentalStatus.Approved);

        // Assert
        Assert.Equal(RentalStatus.Approved, result.Status);
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default), Times.Once);
    }

    [Fact]
    public async Task TransitionAsync_OverdueToReturned_IsLegal()
    {
        // Overdue is the client-side derived state for a late OutForRent rental.
        // The borrower must still be able to mark it Returned.
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Returned, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Returned, DateTime.UtcNow));

        // Act
        var result = await svc.TransitionAsync(7, RentalStatus.Overdue, RentalStatus.Returned);

        // Assert
        Assert.Equal(RentalStatus.Returned, result.Status);
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Returned, default), Times.Once);
    }

    [Fact]
    public async Task TransitionAsync_Throws_OnIllegalTransition_WithoutCallingRepo()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionAsync(7, RentalStatus.Completed, RentalStatus.Approved));

        repo.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RentalStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task TransitionAsync_BubblesRepoExceptions()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.TransitionAsync(7, RentalStatus.Requested, RentalStatus.Approved));

        // Assert
        Assert.Contains("network down", ex.Message);
    }

    // ---- Convenience methods (smoke-test one of each) --------------------

    [Fact]
    public async Task ApproveAsync_TransitionsToApproved()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.GetByIdAsync(7, default)).ReturnsAsync(SampleRental(7, RentalStatus.Requested));
        repo.Setup(r => r.GetIncomingAsync(null, default)).ReturnsAsync(Array.Empty<Rental>());
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));

        // Act
        await svc.ApproveAsync(7, RentalStatus.Requested);

        // Assert
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_Throws_WhenConflictingApprovedRentalExists()
    {
        // Two requests for the same item and overlapping dates; one is already Approved.
        // Arrange
        var (svc, repo) = BuildWithMock();
        var toApprove = new Rental { Id = 7, ItemId = 1, BorrowerId = 2,
            StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 5),
            Status = RentalStatus.Requested, TotalPrice = 40m };
        var alreadyApproved = new Rental { Id = 8, ItemId = 1, BorrowerId = 3,
            StartDate = new DateOnly(2026, 6, 3), EndDate = new DateOnly(2026, 6, 7),
            Status = RentalStatus.Approved, TotalPrice = 40m };

        repo.Setup(r => r.GetByIdAsync(7, default)).ReturnsAsync(toApprove);
        repo.Setup(r => r.GetIncomingAsync(null, default))
            .ReturnsAsync(new Rental[] { toApprove, alreadyApproved });

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ApproveAsync(7, RentalStatus.Requested));

        // Assert
        Assert.Contains("committed", ex.Message, StringComparison.OrdinalIgnoreCase);
        repo.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RentalStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_AllowsApproval_WhenOtherConflictingRentalIsOnlyRequested()
    {
        // Two competing Requested rentals for the same item and dates — the first one
        // approved should succeed; a pending Requested rental must not block it.
        // Arrange
        var (svc, repo) = BuildWithMock();
        var toApprove = new Rental { Id = 7, ItemId = 1, BorrowerId = 2,
            StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 5),
            Status = RentalStatus.Requested, TotalPrice = 40m };
        var alsoRequested = new Rental { Id = 8, ItemId = 1, BorrowerId = 3,
            StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 5),
            Status = RentalStatus.Requested, TotalPrice = 40m };

        repo.Setup(r => r.GetByIdAsync(7, default)).ReturnsAsync(toApprove);
        repo.Setup(r => r.GetIncomingAsync(null, default))
            .ReturnsAsync(new Rental[] { toApprove, alsoRequested });
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));

        // Act
        await svc.ApproveAsync(7, RentalStatus.Requested);

        // Assert
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_TransitionsToRejected()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Rejected, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Rejected, DateTime.UtcNow));

        // Act
        await svc.RejectAsync(7, RentalStatus.Requested);

        // Assert
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Rejected, default), Times.Once);
    }

    [Fact]
    public async Task MarkOutForRentAsync_TransitionsFromApproved()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.OutForRent, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.OutForRent, DateTime.UtcNow));

        // Act
        await svc.MarkOutForRentAsync(7, RentalStatus.Approved);

        // Assert
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.OutForRent, default), Times.Once);
    }

    [Fact]
    public async Task MarkReturnedAsync_TransitionsFromOutForRent()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Returned, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Returned, DateTime.UtcNow));

        // Act
        await svc.MarkReturnedAsync(7, RentalStatus.OutForRent);

        // Assert
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Returned, default), Times.Once);
    }

    [Fact]
    public async Task MarkCompletedAsync_TransitionsFromReturned()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Completed, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Completed, DateTime.UtcNow));

        // Act
        await svc.MarkCompletedAsync(7, RentalStatus.Returned);

        // Assert
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Completed, default), Times.Once);
    }

    // ---- Read pass-through -----------------------------------------------

    [Fact]
    public async Task GetRentalAsync_DelegatesToRepo()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var sample = SampleRental(7, RentalStatus.Requested);
        repo.Setup(r => r.GetByIdAsync(7, default)).ReturnsAsync(sample);

        // Act
        var result = await svc.GetRentalAsync(7);

        // Assert
        Assert.Same(sample, result);
    }

    [Fact]
    public async Task GetIncomingAsync_DelegatesToRepo_WithQuery()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var query = new RentalQuery { Status = RentalStatus.Requested };
        repo.Setup(r => r.GetIncomingAsync(query, default))
            .ReturnsAsync(Array.Empty<Rental>());

        // Act
        await svc.GetIncomingAsync(query);

        // Assert
        repo.Verify(r => r.GetIncomingAsync(query, default), Times.Once);
    }

    // ---- Helpers ----------------------------------------------------------

    private static IRentalService Build() =>
        new RentalService(Mock.Of<IRentalRepository>());

    private static (IRentalService svc, Mock<IRentalRepository> repo) BuildWithMock()
    {
        var repo = new Mock<IRentalRepository>();
        return (new RentalService(repo.Object), repo);
    }

    private static Rental Rental(DateOnly start, DateOnly end, RentalStatus status) => new()
    {
        Id = 1,
        ItemId = 1,
        BorrowerId = 2,
        StartDate = start,
        EndDate = end,
        Status = status,
        TotalPrice = 10m,
    };

    private static Rental SampleRental(int id, RentalStatus status) => new()
    {
        Id = id,
        ItemId = 1,
        BorrowerId = 2,
        StartDate = new DateOnly(2026, 5, 1),
        EndDate = new DateOnly(2026, 5, 3),
        Status = status,
        TotalPrice = 30m,
    };
}
