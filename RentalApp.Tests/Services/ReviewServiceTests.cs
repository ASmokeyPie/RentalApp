using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.Tests.Services;

public class ReviewServiceTests
{
    // ---- IsRentalReviewable ----------------------------------------------

    [Fact]
    public void IsRentalReviewable_True_WhenCompletedAndViewerIsBorrower()
    {
        // Arrange
        var svc = Build();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);

        // Act + Assert
        Assert.True(svc.IsRentalReviewable(rental, currentUserId: 2));
    }

    [Theory]
    [InlineData(RentalStatus.Requested)]
    [InlineData(RentalStatus.Approved)]
    [InlineData(RentalStatus.OutForRent)]
    [InlineData(RentalStatus.Returned)]
    [InlineData(RentalStatus.Rejected)]
    public void IsRentalReviewable_False_WhenStatusIsNotCompleted(RentalStatus status)
    {
        // Arrange
        var svc = Build();
        var rental = Rental(7, status, borrowerId: 2);

        // Act + Assert
        Assert.False(svc.IsRentalReviewable(rental, currentUserId: 2));
    }

    [Fact]
    public void IsRentalReviewable_False_WhenViewerIsNotBorrower()
    {
        // Arrange
        var svc = Build();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);

        // Act + Assert
        Assert.False(svc.IsRentalReviewable(rental, currentUserId: 99));
    }

    [Fact]
    public void IsRentalReviewable_False_WhenAnonymous()
    {
        // Arrange
        var svc = Build();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);

        // Act + Assert
        Assert.False(svc.IsRentalReviewable(rental, currentUserId: 0));
    }

    // ---- SubmitReviewAsync — happy path -----------------------------------

    [Fact]
    public async Task SubmitReviewAsync_DelegatesToRepo_OnHappyPath()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);
        repo.Setup(r => r.CreateAsync(7, 5, "Loved it", default))
            .ReturnsAsync(new Review { Id = 99, RentalId = 7, ReviewerId = 2, Rating = 5 });

        // Act
        var review = await svc.SubmitReviewAsync(rental, rating: 5, comment: "Loved it", currentUserId: 2);

        // Assert
        Assert.Equal(99, review.Id);
        repo.Verify(r => r.CreateAsync(7, 5, "Loved it", default), Times.Once);
    }

    [Fact]
    public async Task SubmitReviewAsync_PassesNullComment_WhenOmitted()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);
        repo.Setup(r => r.CreateAsync(7, 4, null, default))
            .ReturnsAsync(new Review { Id = 99 });

        // Act
        await svc.SubmitReviewAsync(rental, rating: 4, comment: null, currentUserId: 2);

        // Assert
        repo.Verify(r => r.CreateAsync(7, 4, null, default), Times.Once);
    }

    // ---- SubmitReviewAsync — validation failures --------------------------

    [Fact]
    public async Task SubmitReviewAsync_Throws_WhenRentalNotCompleted()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Approved, borrowerId: 2);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitReviewAsync(rental, 5, "ok", currentUserId: 2));

        // Assert
        Assert.Contains("Completed", ex.Message);
        repo.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task SubmitReviewAsync_Throws_WhenViewerIsNotBorrower()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitReviewAsync(rental, 5, "ok", currentUserId: 99));

        // Assert
        Assert.Contains("borrower", ex.Message, StringComparison.OrdinalIgnoreCase);
        repo.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public async Task SubmitReviewAsync_Throws_WhenRatingOutOfRange(int rating)
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitReviewAsync(rental, rating, "ok", currentUserId: 2));

        // Assert
        Assert.Contains("1 and 5", ex.Message);
        repo.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task SubmitReviewAsync_Throws_WhenCommentTooLong()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);
        var longComment = new string('a', 501);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitReviewAsync(rental, 5, longComment, currentUserId: 2));

        // Assert
        Assert.Contains("500", ex.Message);
        repo.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task SubmitReviewAsync_Throws_WhenAnonymous()
    {
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitReviewAsync(rental, 5, "ok", currentUserId: 0));

        repo.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task SubmitReviewAsync_BubblesRepoExceptions()
    {
        // 409 already-reviewed surfaces as HttpRequestException from the repo.
        // Arrange
        var (svc, repo) = BuildWithMock();
        var rental = Rental(7, RentalStatus.Completed, borrowerId: 2);
        repo.Setup(r => r.CreateAsync(7, 5, "ok", default))
            .ThrowsAsync(new HttpRequestException("Review already exists for this rental."));

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => svc.SubmitReviewAsync(rental, 5, "ok", currentUserId: 2));

        // Assert
        Assert.Contains("already exists", ex.Message);
    }

    // ---- Helpers ---------------------------------------------------------

    private static IReviewService Build() =>
        new ReviewService(Mock.Of<IReviewRepository>());

    private static (IReviewService svc, Mock<IReviewRepository> repo) BuildWithMock()
    {
        var repo = new Mock<IReviewRepository>();
        return (new ReviewService(repo.Object), repo);
    }

    private static Rental Rental(int id, RentalStatus status, int borrowerId) => new()
    {
        Id = id,
        ItemId = 100 + id,
        BorrowerId = borrowerId,
        StartDate = new DateOnly(2026, 5, 1),
        EndDate = new DateOnly(2026, 5, 3),
        Status = status,
        TotalPrice = 15m,
    };
}
