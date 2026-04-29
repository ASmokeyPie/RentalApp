using Moq;
using RentalApp.Database.Models;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class WriteReviewViewModelTests
{
    // ---- LoadAsync --------------------------------------------------------

    [Fact]
    public async Task LoadAsync_PopulatesRental_AndCanReview_OnHappyPath()
    {
        var (vm, rentals, reviews, auth, _) = Build();
        var rental = SampleRental(7, RentalStatus.Completed, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(true);

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.NotNull(vm.Rental);
        Assert.True(vm.CanReview);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_OnNotFound_SetsError()
    {
        var (vm, rentals, _, _, _) = Build();
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync((Rental?)null);

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.CanReview);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_LocksForm_WhenStatusNotCompleted()
    {
        var (vm, rentals, reviews, auth, _) = Build();
        var rental = SampleRental(7, RentalStatus.Approved, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(false);

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.CanReview);
        Assert.True(vm.HasError);
        Assert.Contains("Completed", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_LocksForm_WhenViewerIsNotBorrower()
    {
        var (vm, rentals, reviews, auth, _) = Build();
        var rental = SampleRental(7, RentalStatus.Completed, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(99));
        reviews.Setup(s => s.IsRentalReviewable(rental, 99)).Returns(false);

        vm.RentalId = 7;
        await vm.LoadAsync();

        Assert.False(vm.CanReview);
        Assert.True(vm.HasError);
        Assert.Contains("borrower", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Star symbols (live update) --------------------------------------

    [Fact]
    public void StarSymbols_FollowRating()
    {
        var (vm, _, _, _, _) = Build();

        vm.Rating = 3;

        Assert.Equal("★", vm.Star1Symbol);
        Assert.Equal("★", vm.Star2Symbol);
        Assert.Equal("★", vm.Star3Symbol);
        Assert.Equal("☆", vm.Star4Symbol);
        Assert.Equal("☆", vm.Star5Symbol);
    }

    [Fact]
    public void SetRating_ClampsToValidRange()
    {
        // SetRating takes string (CommandParameter from XAML is always string).
        var (vm, _, _, _, _) = Build();
        vm.Rating = 3;

        vm.SetRating("0");      // out of range — ignored
        Assert.Equal(3, vm.Rating);

        vm.SetRating("7");      // out of range — ignored
        Assert.Equal(3, vm.Rating);

        vm.SetRating("not-a-number");  // unparseable — ignored
        Assert.Equal(3, vm.Rating);

        vm.SetRating("5");      // valid
        Assert.Equal(5, vm.Rating);
    }

    // ---- SubmitAsync -----------------------------------------------------

    [Fact]
    public async Task SubmitAsync_DelegatesToService_AndNavigatesBack_OnSuccess()
    {
        var (vm, rentals, reviews, auth, nav) = Build();
        var rental = SampleRental(7, RentalStatus.Completed, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(true);
        reviews.Setup(s => s.SubmitReviewAsync(rental, It.IsAny<int>(), It.IsAny<string?>(), 2, default))
               .ReturnsAsync(new Review { Id = 99 });

        vm.RentalId = 7;
        await vm.LoadAsync();
        vm.Rating = 4;
        vm.Comment = "Great drill";
        await vm.SubmitAsync();

        reviews.Verify(s => s.SubmitReviewAsync(rental, 4, "Great drill", 2, default), Times.Once);
        nav.Verify(n => n.NavigateBackAsync(), Times.Once);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_PassesNullComment_WhenBlank()
    {
        var (vm, rentals, reviews, auth, _) = Build();
        var rental = SampleRental(7, RentalStatus.Completed, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(true);
        reviews.Setup(s => s.SubmitReviewAsync(rental, It.IsAny<int>(), It.IsAny<string?>(), 2, default))
               .ReturnsAsync(new Review { Id = 99 });

        vm.RentalId = 7;
        await vm.LoadAsync();
        vm.Comment = "   ";   // whitespace-only → null
        await vm.SubmitAsync();

        reviews.Verify(s => s.SubmitReviewAsync(rental, It.IsAny<int>(), null, 2, default), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_DoesNothing_WhenNotReviewable()
    {
        var (vm, rentals, reviews, auth, nav) = Build();
        var rental = SampleRental(7, RentalStatus.Approved, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(false);

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.SubmitAsync();

        reviews.Verify(s => s.SubmitReviewAsync(
                It.IsAny<Rental>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>(), default),
            Times.Never);
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_OnServiceValidationError_SetsError_AndDoesNotNavigate()
    {
        var (vm, rentals, reviews, auth, nav) = Build();
        var rental = SampleRental(7, RentalStatus.Completed, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(true);
        reviews.Setup(s => s.SubmitReviewAsync(rental, It.IsAny<int>(), It.IsAny<string?>(), 2, default))
               .ThrowsAsync(new InvalidOperationException("Rating must be between 1 and 5 stars."));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.SubmitAsync();

        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("1 and 5", vm.ErrorMessage);
    }

    [Fact]
    public async Task SubmitAsync_OnServerConflict_SurfacesMessage()
    {
        // 409 already-reviewed surfaces as HttpRequestException through the repo.
        var (vm, rentals, reviews, auth, nav) = Build();
        var rental = SampleRental(7, RentalStatus.Completed, borrowerId: 2);
        rentals.Setup(s => s.GetRentalAsync(7, default)).ReturnsAsync(rental);
        auth.SetupGet(a => a.CurrentUser).Returns(User(2));
        reviews.Setup(s => s.IsRentalReviewable(rental, 2)).Returns(true);
        reviews.Setup(s => s.SubmitReviewAsync(rental, It.IsAny<int>(), It.IsAny<string?>(), 2, default))
               .ThrowsAsync(new HttpRequestException("Review already exists for this rental."));

        vm.RentalId = 7;
        await vm.LoadAsync();
        await vm.SubmitAsync();

        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("already exists", vm.ErrorMessage);
    }

    // ---- Helpers ---------------------------------------------------------

    private static (
        WriteReviewViewModel vm,
        Mock<IRentalService> rentals,
        Mock<IReviewService> reviews,
        Mock<IAuthenticationService> auth,
        Mock<INavigationService> navigation) Build()
    {
        var rentals = new Mock<IRentalService>();
        var reviews = new Mock<IReviewService>();
        var auth = new Mock<IAuthenticationService>();
        var nav = new Mock<INavigationService>();
        var vm = new WriteReviewViewModel(rentals.Object, reviews.Object, auth.Object, nav.Object);
        return (vm, rentals, reviews, auth, nav);
    }

    private static User User(int id) => new()
    {
        Id = id,
        Email = $"u{id}@b.c",
        FirstName = "U",
        LastName = $"{id}",
    };

    private static Rental SampleRental(int id, RentalStatus status, int borrowerId) => new()
    {
        Id = id,
        ItemId = 100 + id,
        BorrowerId = borrowerId,
        StartDate = new DateOnly(2026, 5, 1),
        EndDate = new DateOnly(2026, 5, 3),
        Status = status,
        TotalPrice = 15m,
        ItemTitle = "Drill",
    };
}
