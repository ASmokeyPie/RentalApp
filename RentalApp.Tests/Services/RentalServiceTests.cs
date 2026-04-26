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
        var svc = Build();
        var price = svc.CalculatePrice(10m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 1));
        Assert.Equal(10m, price);
    }

    [Fact]
    public void CalculatePrice_MultiDay_ChargesInclusiveDays()
    {
        // 1st through 3rd inclusive = 3 days at £5 = £15
        var svc = Build();
        var price = svc.CalculatePrice(5m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));
        Assert.Equal(15m, price);
    }

    [Fact]
    public void CalculatePrice_ZeroRate_ProducesZero()
    {
        var svc = Build();
        var price = svc.CalculatePrice(0m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
        Assert.Equal(0m, price);
    }

    [Fact]
    public void CalculatePrice_EndBeforeStart_Throws()
    {
        var svc = Build();
        Assert.Throws<ArgumentException>(
            () => svc.CalculatePrice(5m, new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 1)));
    }

    // ---- IsTransitionLegal ------------------------------------------------

    [Theory]
    // From Requested
    [InlineData(RentalStatus.Requested, RentalStatus.Approved,    true)]
    [InlineData(RentalStatus.Requested, RentalStatus.Rejected,    true)]
    [InlineData(RentalStatus.Requested, RentalStatus.Cancelled,   true)]
    [InlineData(RentalStatus.Requested, RentalStatus.OutForRent,  false)]
    [InlineData(RentalStatus.Requested, RentalStatus.Returned,    false)]
    [InlineData(RentalStatus.Requested, RentalStatus.Completed,   false)]
    // From Approved
    [InlineData(RentalStatus.Approved,  RentalStatus.OutForRent,  true)]
    [InlineData(RentalStatus.Approved,  RentalStatus.Cancelled,   true)]
    [InlineData(RentalStatus.Approved,  RentalStatus.Rejected,    false)]
    [InlineData(RentalStatus.Approved,  RentalStatus.Completed,   false)]
    // From OutForRent
    [InlineData(RentalStatus.OutForRent, RentalStatus.Returned,   true)]
    [InlineData(RentalStatus.OutForRent, RentalStatus.Cancelled,  false)]
    [InlineData(RentalStatus.OutForRent, RentalStatus.Completed,  false)]
    // From Returned
    [InlineData(RentalStatus.Returned, RentalStatus.Completed,    true)]
    [InlineData(RentalStatus.Returned, RentalStatus.OutForRent,   false)]
    // Terminal states have no outgoing edges
    [InlineData(RentalStatus.Rejected,  RentalStatus.Approved,    false)]
    [InlineData(RentalStatus.Cancelled, RentalStatus.Approved,    false)]
    [InlineData(RentalStatus.Completed, RentalStatus.Approved,    false)]
    // Self-transition not legal
    [InlineData(RentalStatus.Requested, RentalStatus.Requested,   false)]
    public void IsTransitionLegal_RespectsStateMachine(RentalStatus from, RentalStatus to, bool expected)
    {
        var svc = Build();
        Assert.Equal(expected, svc.IsTransitionLegal(from, to));
    }

    // ---- HasOverlap -------------------------------------------------------

    [Fact]
    public void HasOverlap_NoOverlap_WhenRangesAreDisjoint()
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), RentalStatus.Approved) };
        Assert.False(svc.HasOverlap(existing, new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 6)));
    }

    [Fact]
    public void HasOverlap_OverlapsOnExactMatch()
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), RentalStatus.Approved) };
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3)));
    }

    [Fact]
    public void HasOverlap_OverlapsOnPartialLeft()
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 10), RentalStatus.Approved) };
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 3), new DateOnly(2026, 5, 6)));
    }

    [Fact]
    public void HasOverlap_OverlapsOnPartialRight()
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 10), RentalStatus.Approved) };
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 9), new DateOnly(2026, 5, 12)));
    }

    [Fact]
    public void HasOverlap_OverlapsWhenContained()
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30), RentalStatus.Approved) };
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12)));
    }

    [Fact]
    public void HasOverlap_OverlapsWhenContaining()
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12), RentalStatus.Approved) };
        Assert.True(svc.HasOverlap(existing, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30)));
    }

    [Theory]
    [InlineData(RentalStatus.Rejected)]
    [InlineData(RentalStatus.Cancelled)]
    [InlineData(RentalStatus.Completed)]
    public void HasOverlap_IgnoresTerminalStateRentals(RentalStatus terminalStatus)
    {
        var svc = Build();
        var existing = new[] { Rental(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30), terminalStatus) };
        Assert.False(svc.HasOverlap(existing, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12)));
    }

    [Fact]
    public void HasOverlap_HandlesNullCollection()
    {
        var svc = Build();
        Assert.False(svc.HasOverlap(null!, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3)));
    }

    // ---- RequestRentalAsync ----------------------------------------------

    [Fact]
    public async Task RequestRentalAsync_DelegatesToRepo_OnHappyPath()
    {
        var (svc, repo) = BuildWithMock();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(7);
        var ret = future.AddDays(2);
        repo.Setup(r => r.RequestAsync(42, future, ret, default))
            .ReturnsAsync(SampleRental(99, RentalStatus.Requested));

        var rental = await svc.RequestRentalAsync(42, future, ret);

        Assert.Equal(99, rental.Id);
        repo.Verify(r => r.RequestAsync(42, future, ret, default), Times.Once);
    }

    [Fact]
    public async Task RequestRentalAsync_Throws_WhenEndBeforeStart()
    {
        var (svc, repo) = BuildWithMock();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RequestRentalAsync(42, new DateOnly(2027, 5, 5), new DateOnly(2027, 5, 1)));

        Assert.Contains("End date", ex.Message);
        repo.Verify(r => r.RequestAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default), Times.Never);
    }

    [Fact]
    public async Task RequestRentalAsync_Throws_WhenStartInPast()
    {
        var (svc, repo) = BuildWithMock();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RequestRentalAsync(42, past, past.AddDays(2)));

        Assert.Contains("past", ex.Message, StringComparison.OrdinalIgnoreCase);
        repo.Verify(r => r.RequestAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default), Times.Never);
    }

    // ---- TransitionAsync --------------------------------------------------

    [Fact]
    public async Task TransitionAsync_DelegatesToRepo_OnLegalTransition()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));

        var result = await svc.TransitionAsync(7, RentalStatus.Requested, RentalStatus.Approved);

        Assert.Equal(RentalStatus.Approved, result.Status);
        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default), Times.Once);
    }

    [Fact]
    public async Task TransitionAsync_Throws_OnIllegalTransition_WithoutCallingRepo()
    {
        var (svc, repo) = BuildWithMock();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionAsync(7, RentalStatus.Completed, RentalStatus.Approved));

        repo.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RentalStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task TransitionAsync_BubblesRepoExceptions()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ThrowsAsync(new HttpRequestException("network down"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.TransitionAsync(7, RentalStatus.Requested, RentalStatus.Approved));

        Assert.Contains("network down", ex.Message);
    }

    // ---- Convenience methods (smoke-test one of each) --------------------

    [Fact]
    public async Task ApproveAsync_TransitionsToApproved()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));

        await svc.ApproveAsync(7, RentalStatus.Requested);

        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Approved, default), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_TransitionsToRejected()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Rejected, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Rejected, DateTime.UtcNow));

        await svc.RejectAsync(7, RentalStatus.Requested);

        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Rejected, default), Times.Once);
    }

    [Fact]
    public async Task MarkOutForRentAsync_TransitionsFromApproved()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.OutForRent, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.OutForRent, DateTime.UtcNow));

        await svc.MarkOutForRentAsync(7, RentalStatus.Approved);

        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.OutForRent, default), Times.Once);
    }

    [Fact]
    public async Task MarkReturnedAsync_TransitionsFromOutForRent()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Returned, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Returned, DateTime.UtcNow));

        await svc.MarkReturnedAsync(7, RentalStatus.OutForRent);

        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Returned, default), Times.Once);
    }

    [Fact]
    public async Task MarkCompletedAsync_TransitionsFromReturned()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Completed, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Completed, DateTime.UtcNow));

        await svc.MarkCompletedAsync(7, RentalStatus.Returned);

        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Completed, default), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_TransitionsFromRequested()
    {
        var (svc, repo) = BuildWithMock();
        repo.Setup(r => r.UpdateStatusAsync(7, RentalStatus.Cancelled, default))
            .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Cancelled, DateTime.UtcNow));

        await svc.CancelAsync(7, RentalStatus.Requested);

        repo.Verify(r => r.UpdateStatusAsync(7, RentalStatus.Cancelled, default), Times.Once);
    }

    // ---- Read pass-through -----------------------------------------------

    [Fact]
    public async Task GetRentalAsync_DelegatesToRepo()
    {
        var (svc, repo) = BuildWithMock();
        var sample = SampleRental(7, RentalStatus.Requested);
        repo.Setup(r => r.GetByIdAsync(7, default)).ReturnsAsync(sample);

        var result = await svc.GetRentalAsync(7);

        Assert.Same(sample, result);
    }

    [Fact]
    public async Task GetIncomingAsync_DelegatesToRepo_WithQuery()
    {
        var (svc, repo) = BuildWithMock();
        var query = new RentalQuery { Status = RentalStatus.Requested };
        repo.Setup(r => r.GetIncomingAsync(query, default))
            .ReturnsAsync(Array.Empty<Rental>());

        await svc.GetIncomingAsync(query);

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
