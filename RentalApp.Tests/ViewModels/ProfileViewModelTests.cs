using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class ProfileViewModelTests
{
    // ---- RefreshAsync — happy path ----------------------------------------

    [Fact]
    public async Task RefreshAsync_LoadsCurrentUser_FromAuthService()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(42, "Ada", "Lovelace"));
        reviews.Setup(r => r.GetForUserAsync(42, 1, 50, default))
               .ReturnsAsync(PagedResult(Array.Empty<Review>(), total: 0));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.NotNull(vm.CurrentUser);
        Assert.Equal(42, vm.CurrentUser!.Id);
        Assert.Equal("Ada", vm.CurrentUser.FirstName);
    }

    [Fact]
    public async Task RefreshAsync_CallsRefreshCurrentUser_BeforeReadingUser()
    {
        // Arrange — verifies the VM asks for a fresh /users/me on every load
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(Array.Empty<Review>(), total: 0));

        // Act
        await vm.RefreshAsync();

        // Assert
        auth.Verify(a => a.RefreshCurrentUserAsync(), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_PopulatesReviews_FromRepository()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(new[]
               {
                   Review(10, "Drill",  4, "Great tool"),
                   Review(11, "Ladder", 5, null),
               }, total: 2));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Equal(2, vm.Reviews.Count);
        Assert.Equal("Drill",  vm.Reviews[0].ItemTitle);
        Assert.Equal("Ladder", vm.Reviews[1].ItemTitle);
    }

    [Fact]
    public async Task RefreshAsync_UsesAverageRating_FromCurrentUser()
    {
        // Arrange — AverageRating comes from the server via /users/me,
        // not computed locally from the reviews list.
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1, averageRating: 4.3));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(new[]
               {
                   Review(1, "Item A", 4, null),
                   Review(2, "Item B", 5, null),
               }, total: 2));

        // Act
        await vm.RefreshAsync();

        // Assert — the VM uses the server value, not a local average of 4.5
        Assert.Equal(4.3, vm.AverageRating);
        Assert.Equal("4.3 ★", vm.AverageRatingDisplay);
    }

    [Fact]
    public async Task RefreshAsync_SetsAverageRatingNull_WhenUserHasNoRating()
    {
        // Arrange — server returns null averageRating for unrated users
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1, averageRating: null));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(Array.Empty<Review>(), total: 0));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Null(vm.AverageRating);
        Assert.Equal("No ratings yet", vm.AverageRatingDisplay);
    }

    [Fact]
    public async Task RefreshAsync_SetsTotalReviews_FromServerTotalCount()
    {
        // Arrange — server reports 80 total; the first page (50) is what we display
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        var items = Enumerable.Range(1, 50).Select(i => Review(i, $"Item {i}", 4, null)).ToArray();
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(items, total: 80));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Equal(80, vm.TotalReviews);  // authoritative server count
        Assert.Equal(50, vm.Reviews.Count); // first page only
    }

    [Fact]
    public async Task RefreshAsync_SetsIsLoaded_OnSuccess()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(Array.Empty<Review>(), total: 0));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.IsLoaded);
        Assert.False(vm.IsRefreshing);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RefreshAsync_SetsIsEmpty_WhenNoReviews()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(Array.Empty<Review>(), total: 0));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task RefreshAsync_ClearsIsEmpty_WhenReviewsExist()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(new[] { Review(1, "Drill", 5, null) }, total: 1));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.False(vm.IsEmpty);
    }

    // ---- RefreshAsync — replaces stale data on second call ----------------

    [Fact]
    public async Task RefreshAsync_ReplacesReviews_OnSubsequentCalls()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.SetupSequence(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(new[] { Review(1, "Drill", 4, null) }, total: 1))
               .ReturnsAsync(PagedResult(new[]
               {
                   Review(1, "Drill",  4, null),
                   Review(2, "Ladder", 5, null),
               }, total: 2));

        await vm.RefreshAsync(); // first load
        await vm.RefreshAsync(); // second load

        // Assert — no duplicates; second response wins.
        Assert.Equal(2, vm.Reviews.Count);
    }

    // ---- RefreshAsync — RefreshView pre-sets IsRefreshing -----------------

    [Fact]
    public async Task RefreshAsync_ProceedsEvenWhenIsRefreshingAlreadyTrue()
    {
        // Arrange — RefreshView sets this before firing the command
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(1, 1, 50, default))
               .ReturnsAsync(PagedResult(Array.Empty<Review>(), total: 0));

        vm.IsRefreshing = true;

        // Act
        await vm.RefreshAsync();

        // Assert — spinner is cleared and data was loaded
        Assert.False(vm.IsRefreshing);
        reviews.Verify(r => r.GetForUserAsync(1, 1, 50, default), Times.Once);
    }

    // ---- RefreshAsync — error handling ------------------------------------

    [Fact]
    public async Task RefreshAsync_OnException_SetsError_AndClearsSpinner()
    {
        // Arrange
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns(User(1));
        reviews.Setup(r => r.GetForUserAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default))
               .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.HasError);
        Assert.Contains("network down", vm.ErrorMessage);
        Assert.False(vm.IsRefreshing);
        Assert.False(vm.IsLoaded);
    }

    [Fact]
    public async Task RefreshAsync_WhenNoCurrentUser_SetsError_AndDoesNotCallRepository()
    {
        // Arrange — not signed in
        var (vm, auth, reviews, _) = Build();
        auth.Setup(a => a.CurrentUser).Returns((User?)null);

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.HasError);
        Assert.False(vm.IsLoaded);
        reviews.Verify(r => r.GetForUserAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default),
            Times.Never);
    }

    // ---- LogoutAsync ------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_CallsAuthLogout_AndNavigatesToLogin()
    {
        // Arrange
        var (vm, auth, _, nav) = Build();

        // Act
        await vm.LogoutAsync();

        // Assert
        auth.Verify(a => a.LogoutAsync(), Times.Once);
        nav.Verify(n => n.NavigateToAsync("LoginPage"), Times.Once);
    }

    // ---- AverageRatingDisplay computed property ---------------------------

    [Fact]
    public void AverageRatingDisplay_ReturnsFormattedString_WhenRatingSet()
    {
        var (vm, _, _, _) = Build();
        vm.AverageRating = 3.75;
        Assert.Equal("3.8 ★", vm.AverageRatingDisplay);
    }

    [Fact]
    public void AverageRatingDisplay_ReturnsFallback_WhenRatingNull()
    {
        var (vm, _, _, _) = Build();
        vm.AverageRating = null;
        Assert.Equal("No ratings yet", vm.AverageRatingDisplay);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        ProfileViewModel vm,
        Mock<IAuthenticationService> auth,
        Mock<IReviewRepository> reviews,
        Mock<INavigationService> navigation) Build()
    {
        var auth    = new Mock<IAuthenticationService>();
        var reviews = new Mock<IReviewRepository>();
        var nav     = new Mock<INavigationService>();

        // RefreshCurrentUserAsync is a void Task — default Moq behaviour is fine,
        // but set it up explicitly so tests can verify it was called.
        auth.Setup(a => a.RefreshCurrentUserAsync()).Returns(Task.CompletedTask);

        var vm = new ProfileViewModel(auth.Object, reviews.Object, nav.Object);
        return (vm, auth, reviews, nav);
    }

    private static User User(int id, string first = "Test", string last = "User",
                             double? averageRating = null) => new()
    {
        Id            = id,
        FirstName     = first,
        LastName      = last,
        Email         = $"user{id}@example.com",
        PasswordHash  = string.Empty,
        AverageRating = averageRating,
    };

    private static Review Review(int id, string itemTitle, int rating, string? comment) => new()
    {
        Id         = id,
        RentalId   = 100 + id,
        ReviewerId = 1,
        Rating     = rating,
        Comment    = comment,
        ItemTitle  = itemTitle,
        CreatedAt  = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(id - 1),
    };

    private static PagedResult<Review> PagedResult(IEnumerable<Review> items, int total) =>
        new()
        {
            Items      = items.ToList(),
            Page       = 1,
            PageSize   = 50,
            TotalCount = total,
        };
}
