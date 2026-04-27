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
    [InlineData(RentalStatus.Requested,  true,  true,  false, false, false)]
    [InlineData(RentalStatus.Approved,   false, false, true,  false, false)]
    [InlineData(RentalStatus.OutForRent, false, false, false, true,  false)]
    [InlineData(RentalStatus.Returned,   false, false, false, false, true)]
    [InlineData(RentalStatus.Completed,  false, false, false, false, false)]
    [InlineData(RentalStatus.Cancelled,  false, false, false, false, false)]
    [InlineData(RentalStatus.Rejected,   false, false, false, false, false)]
    public async Task OwnerActionFlags_FollowStatus(
        RentalStatus status,
        bool canApprove,
        bool canReject,
        bool canMarkOutForRent,
        bool canMarkReturned,
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
        Assert.Equal(canMarkReturned,   vm.CanMarkReturned);
        Assert.Equal(canMarkCompleted,  vm.CanMarkCompleted);
        Assert.False(vm.CanCancel); // owner can't cancel
    }

    [Theory]
    [InlineData(RentalStatus.Requested,  true)]
    [InlineData(RentalStatus.Approved,   true)]
    [InlineData(RentalStatus.OutForRent, false)]
    [InlineData(RentalStatus.Returned,   false)]
    [InlineData(RentalStatus.Completed,  false)]
    [InlineData(RentalStatus.Rejected,   false)]
    [InlineData(RentalStatus.Cancelled,  false)]
    public async Task BorrowerCancelFlag_FollowsStatus(RentalStatus status, bool canCancel)
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", status, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.Equal(canCancel, vm.CanCancel);
        // Borrower has none of the owner-only flags.
        Assert.False(vm.CanApprove);
        Assert.False(vm.CanReject);
        Assert.False(vm.CanMarkOutForRent);
        Assert.False(vm.CanMarkReturned);
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
    public async Task CancelAsync_BorrowerPath_Works()
    {
        var (vm, rentals, auth) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default))
               .ReturnsAsync(SampleRental(7, "Drill", RentalStatus.Requested, ownerId: 1, borrowerId: 2));
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        rentals.Setup(s => s.CancelAsync(7, RentalStatus.Requested, default))
               .ReturnsAsync(new RentalStatusUpdate(7, RentalStatus.Cancelled, DateTime.UtcNow));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.CancelAsync();

        Assert.Equal(RentalStatus.Cancelled, vm.Rental!.Status);
        Assert.False(vm.CanCancel);          // can't cancel a cancelled rental
        Assert.False(vm.HasAnyAction);
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
    public async Task FullOwnerWorkflow_StateTransitionsThroughEveryStep()
    {
        // Walks the full happy-path: Requested → Approved → OutForRent → Returned → Completed.
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

    // ---- Helpers ----------------------------------------------------------

    private static (
        RentalDetailsViewModel vm,
        Mock<IRentalService> rentals,
        Mock<IAuthenticationService> auth) Build()
    {
        var rentals = new Mock<IRentalService>();
        var auth = new Mock<IAuthenticationService>();
        var vm = new RentalDetailsViewModel(rentals.Object, auth.Object);
        return (vm, rentals, auth);
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
