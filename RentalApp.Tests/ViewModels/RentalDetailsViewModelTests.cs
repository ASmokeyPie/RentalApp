using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class RentalDetailsViewModelTests
{
    // ---- LoadAsync --------------------------------------------------------

    [Fact]
    public async Task LoadAsync_PopulatesRental_AndUpdatesTitle_OnSuccess()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.NotNull(vm.Rental);
        Assert.Equal(7, vm.Rental!.Id);
        Assert.True(vm.IsLoaded);
        Assert.Equal("Rental: Drill", vm.Title);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_OnNotFound_SetsError()
    {
        var (vm, rentals, _) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync((Rental?)null);

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.IsLoaded);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_ProceedsEvenWhenIsRefreshingAlreadyTrue()
    {
        // Same regression as the other refresh-driven VMs.
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.IsRefreshing = true;
        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.IsRefreshing);
        Assert.True(vm.IsLoaded);
    }

    // ---- Role detection ---------------------------------------------------

    [Fact]
    public async Task IsOwner_TrueWhenViewerIsOwner()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.True(vm.IsOwner);
        Assert.False(vm.IsBorrower);
    }

    [Fact]
    public async Task IsBorrower_TrueWhenViewerIsBorrower()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.IsOwner);
        Assert.True(vm.IsBorrower);
    }

    [Fact]
    public async Task BothFalse_WhenViewerIsThirdParty()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(99));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.IsOwner);
        Assert.False(vm.IsBorrower);
        Assert.False(vm.HasAnyAction);
    }

    // ---- Owner action permissions (truth table) --------------------------

    [Theory]
    // status,                          canApprove, canReject, canMarkOut, canMarkCompleted
    [InlineData(RentalStatus.Requested,  true,  true,  false, false)]
    [InlineData(RentalStatus.Approved,   false, false, true,  false)]
    [InlineData(RentalStatus.OutForRent, false, false, false, false)]   // Mark Returned is borrower-only
    [InlineData(RentalStatus.Returned,   false, false, false, true)]
    [InlineData(RentalStatus.Completed,  false, false, false, false)]
    [InlineData(RentalStatus.Rejected,   false, false, false, false)]
    public async Task OwnerActionFlags_FollowStatus(
        RentalStatus status,
        bool canApprove,
        bool canReject,
        bool canMarkOutForRent,
        bool canMarkCompleted)
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", status, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.Equal(canApprove,        vm.CanApprove);
        Assert.Equal(canReject,         vm.CanReject);
        Assert.Equal(canMarkOutForRent, vm.CanMarkOutForRent);
        Assert.Equal(canMarkCompleted,  vm.CanMarkCompleted);
        // Owner does NOT mark Returned (the borrower does, per server rule).
        Assert.False(vm.CanMarkReturned);
    }

    [Theory]
    // status,                          canMarkReturned
    [InlineData(RentalStatus.Requested,  false)]
    [InlineData(RentalStatus.Approved,   false)]
    [InlineData(RentalStatus.OutForRent, true)]   // ← borrower marks Returned
    [InlineData(RentalStatus.Returned,   false)]
    [InlineData(RentalStatus.Completed,  false)]
    [InlineData(RentalStatus.Rejected,   false)]
    public async Task BorrowerActionFlags_FollowStatus(
        RentalStatus status,
        bool canMarkReturned)
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", status, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.Equal(canMarkReturned, vm.CanMarkReturned);
        // Borrower has none of the owner-only flags.
        Assert.False(vm.CanApprove);
        Assert.False(vm.CanReject);
        Assert.False(vm.CanMarkOutForRent);
        Assert.False(vm.CanMarkCompleted);
    }

    // ---- Action commands --------------------------------------------------

    [Fact]
    public async Task ApproveAsync_DelegatesToService_AndPatchesStatus()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));
        rentals.Setup(s => s.ApproveAsync(7, RentalStatus.Requested, default))
               .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.ApproveAsync();

        Assert.Equal(RentalStatus.Approved, vm.Rental!.Status);
        Assert.True(vm.CanMarkOutForRent);   // derived flags re-evaluated
        Assert.False(vm.CanApprove);
    }

    [Fact]
    public async Task ActionAsync_OnException_SetsError_AndKeepsStatus()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));
        rentals.Setup(s => s.ApproveAsync(7, RentalStatus.Requested, default))
               .ThrowsAsync(new HttpRequestException("server error"));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.ApproveAsync();

        Assert.Equal(RentalStatus.Requested, vm.Rental!.Status);  // unchanged
        Assert.True(vm.HasError);
        Assert.Contains("server error", vm.ErrorMessage);
        Assert.False(vm.IsWorking);
    }

    [Fact]
    public async Task FullWorkflow_StateTransitionsThroughEveryStep()
    {
        // Walks the full happy-path: Requested → Approved → OutForRent → Returned → Completed.
        // The action commands themselves don't gate on the Can* flags (those
        // drive UI button visibility); they delegate straight to the service.
        // So this test verifies the state-mutation chain regardless of which
        // role would actually trigger each step in the real UI (Mark Returned
        // is borrower-only, the rest are owner).
        var (vm, rentals, auth) = Build();
        var sample = SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(sample);
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));
        rentals.Setup(s => s.ApproveAsync(7, RentalStatus.Requested, default))
               .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Approved, DateTime.UtcNow));
        rentals.Setup(s => s.MarkOutForRentAsync(7, RentalStatus.Approved, default))
               .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.OutForRent, DateTime.UtcNow));
        rentals.Setup(s => s.MarkReturnedAsync(7, RentalStatus.OutForRent, default))
               .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Returned, DateTime.UtcNow));
        rentals.Setup(s => s.MarkCompletedAsync(7, RentalStatus.Returned, default))
               .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Completed, DateTime.UtcNow));

        vm.RentalId = 7;
        await vm.LoadAsync();

        await vm.ApproveAsync();
        Assert.Equal(RentalStatus.Approved, vm.Rental!.Status);

        await vm.MarkOutForRentAsync();
        Assert.Equal(RentalStatus.OutForRent, vm.Rental.Status);

        await vm.MarkReturnedAsync();
        Assert.Equal(RentalStatus.Returned, vm.Rental.Status);

        await vm.MarkCompletedAsync();
        Assert.Equal(RentalStatus.Completed, vm.Rental.Status);

        Assert.False(vm.HasAnyAction);  // terminal — no further moves
    }

    // ---- CanLeaveReview / LeaveReviewAsync -------------------------------

    [Fact]
    public async Task CanLeaveReview_True_ForBorrowerOnCompletedRental()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Completed, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.True(vm.CanLeaveReview);
        Assert.True(vm.HasAnyAction);
    }

    [Theory]
    [InlineData(RentalStatus.Requested)]
    [InlineData(RentalStatus.Approved)]
    [InlineData(RentalStatus.OutForRent)]
    [InlineData(RentalStatus.Returned)]
    [InlineData(RentalStatus.Rejected)]
    public async Task CanLeaveReview_False_ForBorrowerOnNonCompletedRental(RentalStatus status)
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", status, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.CanLeaveReview);
    }

    [Fact]
    public async Task CanLeaveReview_False_ForOwnerOnCompletedRental()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Completed, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.CanLeaveReview);
    }

    [Fact]
    public async Task LeaveReviewAsync_NavigatesToWriteReviewPage_WhenAllowed()
    {
        var (vm, rentals, auth, nav) = BuildWithNav();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Completed, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.LeaveReviewAsync();

        nav.Verify(n => n.NavigateToAsync(
                "WriteReviewPage",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("rentalId") && (int)d["rentalId"] == 7)),
            Times.Once);
    }

    [Fact]
    public async Task LeaveReviewAsync_DoesNothing_WhenNotAllowed()
    {
        // Owner viewing own rental — CanLeaveReview is false.
        var (vm, rentals, auth, nav) = BuildWithNav();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Completed, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(1));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.LeaveReviewAsync();

        nav.Verify(n => n.NavigateToAsync("WriteReviewPage", It.IsAny<Dictionary<string, object>>()),
            Times.Never);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        RentalDetailsViewModel vm,
        Mock<IRentalService> rentals,
        Mock<IAuthenticationService> auth) Build()
    {
        var (vm, rentals, auth, _) = BuildWithNav();
        return (vm, rentals, auth);
    }

    private static (
        RentalDetailsViewModel vm,
        Mock<IRentalService> rentals,
        Mock<IAuthenticationService> auth,
        Mock<INavigationService> navigation) BuildWithNav()
    {
        var rentals = new Mock<IRentalService>();
        var auth = new Mock<IAuthenticationService>();
        var nav = new Mock<INavigationService>();
        var vm = new RentalDetailsViewModel(rentals.Object, auth.Object, nav.Object);
        return (vm, rentals, auth, nav);
    }

    private static User User(int id) => new()
    {
        Id = id,
        Email = $"u{id}@b.c",
        FirstName = "U",
        LastName = $"{id}",
    };

    private static Rental SampleRental(int id, string itemTitle, RentalStatus status, int ownerId, int borrowerId) =>
        new()
        {
            Id = id,
            ItemId = 100 + id,
            BorrowerId = borrowerId,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 3),
            Status = status,
            TotalPrice = 15m,
            ItemTitle = itemTitle,
            BorrowerName = "Bob",
            OwnerId = ownerId,
            OwnerName = "Ada",
        };
}
