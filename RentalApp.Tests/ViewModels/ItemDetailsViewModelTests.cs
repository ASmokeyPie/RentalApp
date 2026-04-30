using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class ItemDetailsViewModelTests
{
    [Fact]
    public async Task LoadAsync_ProceedsEvenWhenIsRefreshingAlreadyTrue()
    {
        // Regression: RefreshView toggles IsRefreshing=true BEFORE firing the
        // command. An early-return on IsRefreshing would leave the spinner
        // stuck. LoadAsync must still run and clear the flag in finally.
        var (vm, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill"));

        vm.IsRefreshing = true;            // simulate the RefreshView pre-set
        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.IsRefreshing);     // cleared in finally
        Assert.True(vm.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_NoOps_WhenItemIdIsZero()
    {
        var (vm, repo, _, _, _) = Build();

        await vm.LoadAsync();

        repo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
        Assert.False(vm.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_PopulatesItem_OnSuccess_AndUpdatesTitle()
    {
        var (vm, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Cordless Drill"));

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.NotNull(vm.Item);
        Assert.Equal(42, vm.Item!.Id);
        Assert.True(vm.IsLoaded);
        Assert.False(vm.HasError);
        Assert.Equal("Cordless Drill", vm.Title);
    }

    [Fact]
    public async Task LoadAsync_OnNotFound_SetsError_AndIsLoadedStaysFalse()
    {
        var (vm, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync((Item?)null);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Null(vm.Item);
        Assert.False(vm.IsLoaded);
        Assert.False(vm.IsOwner);
        Assert.True(vm.HasError);
        Assert.Contains("could not be found", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_OnException_SetsError_AndIsLoadedStaysFalse()
    {
        var (vm, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(7, default))
            .ThrowsAsync(new HttpRequestException("network down"));

        vm.ItemId = 7;
        await vm.LoadAsync();

        Assert.Null(vm.Item);
        Assert.False(vm.IsLoaded);
        Assert.False(vm.IsOwner);
        Assert.True(vm.HasError);
        Assert.Contains("network down", vm.ErrorMessage);
    }

    [Fact]
    public async Task SettingItemId_TriggersAutomaticLoad()
    {
        // OnItemIdChanged fires LoadAsync as fire-and-forget. We can't await
        // it directly, so verify the side-effect by asserting the repo call
        // happened before the await suspension yields back.
        var (vm, repo, _, _, _) = Build();
        var tcs = new TaskCompletionSource<Item?>();
        repo.Setup(r => r.GetByIdAsync(42, default)).Returns(tcs.Task);

        vm.ItemId = 42;

        repo.Verify(r => r.GetByIdAsync(42, default), Times.Once);

        // Complete the load so we leave the test in a clean state.
        tcs.SetResult(SampleItem(42, "Drill"));
        await Task.Yield();
    }

    // ---- IsOwner ----------------------------------------------------------

    [Fact]
    public async Task LoadAsync_SetsIsOwnerTrue_WhenCurrentUserOwnsItem()
    {
        var (vm, repo, auth, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.True(vm.IsOwner);
    }

    [Fact]
    public async Task LoadAsync_SetsIsOwnerFalse_WhenCurrentUserIsNotOwner()
    {
        var (vm, repo, auth, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.IsOwner);
    }

    [Fact]
    public async Task LoadAsync_SetsIsOwnerFalse_WhenAnonymous()
    {
        var (vm, repo, auth, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns((User?)null);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.IsOwner);
    }

    // ---- EditItemAsync ----------------------------------------------------

    [Fact]
    public async Task EditItemAsync_NavigatesToEditPage_WhenOwner()
    {
        var (vm, repo, auth, nav, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();
        await vm.EditItemAsync();

        nav.Verify(n => n.NavigateToAsync(
                "EditItemPage",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("itemId") && (int)d["itemId"] == 42)),
            Times.Once);
    }

    [Fact]
    public async Task EditItemAsync_DoesNothing_WhenNotOwner()
    {
        var (vm, repo, auth, nav, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();
        await vm.EditItemAsync();

        nav.Verify(n => n.NavigateToAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
            Times.Never);
    }

    // ---- CanRent / RentItemAsync ------------------------------------------

    [Fact]
    public async Task LoadAsync_SetsCanRent_WhenAuthenticatedNonOwnerAvailableItem()
    {
        var (vm, repo, auth, _, _) = Build();
        var item = SampleItem(42, "Drill", ownerId: 7);
        item.IsAvailable = true;
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(item);
        auth.SetupGet(a => a.IsAuthenticated).Returns(true);
        auth.SetupGet(a => a.CurrentUser).Returns(
            new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.True(vm.CanRent);
    }

    [Fact]
    public async Task LoadAsync_CanRentFalse_WhenViewerIsOwner()
    {
        var (vm, repo, auth, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.IsAuthenticated).Returns(true);
        auth.SetupGet(a => a.CurrentUser).Returns(
            new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.CanRent);
    }

    [Fact]
    public async Task LoadAsync_CanRentFalse_WhenItemNotAvailable()
    {
        var (vm, repo, auth, _, _) = Build();
        var item = SampleItem(42, "Drill", ownerId: 7);
        item.IsAvailable = false;
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(item);
        auth.SetupGet(a => a.IsAuthenticated).Returns(true);
        auth.SetupGet(a => a.CurrentUser).Returns(
            new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.CanRent);
    }

    [Fact]
    public async Task LoadAsync_CanRentFalse_WhenNotAuthenticated()
    {
        var (vm, repo, auth, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.IsAuthenticated).Returns(false);
        auth.SetupGet(a => a.CurrentUser).Returns((User?)null);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.CanRent);
    }

    [Fact]
    public async Task RentItemAsync_NavigatesToRequestRentalPage_WhenCanRent()
    {
        var (vm, repo, auth, nav, _) = Build();
        var item = SampleItem(42, "Drill", ownerId: 7);
        item.IsAvailable = true;
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(item);
        auth.SetupGet(a => a.IsAuthenticated).Returns(true);
        auth.SetupGet(a => a.CurrentUser).Returns(
            new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();
        await vm.RentItemAsync();

        nav.Verify(n => n.NavigateToAsync(
                "RequestRentalPage",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("itemId") && (int)d["itemId"] == 42)),
            Times.Once);
    }

    [Fact]
    public async Task RentItemAsync_DoesNothing_WhenCannotRent()
    {
        var (vm, repo, auth, nav, _) = Build();
        // Owner viewing own item — CanRent will be false
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.IsAuthenticated).Returns(true);
        auth.SetupGet(a => a.CurrentUser).Returns(
            new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();
        await vm.RentItemAsync();

        nav.Verify(n => n.NavigateToAsync("RequestRentalPage", It.IsAny<Dictionary<string, object>>()),
            Times.Never);
    }

    // ---- Reviews ----------------------------------------------------------

    [Fact]
    public async Task LoadAsync_PopulatesReviews_FromDedicatedEndpoint()
    {
        var (vm, repo, _, _, reviews) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(SampleItem(42, "Drill"));
        var page = new PagedResult<Review>
        {
            Items = new[] { SampleReview(1), SampleReview(2) },
            Page = 1,
            PageSize = 50,
            TotalCount = 2,
        };
        reviews.Setup(r => r.GetForItemAsync(42, 1, 50, default)).ReturnsAsync(page);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Equal(2, vm.Reviews.Count);
        Assert.Equal(2, vm.TotalReviews);
    }

    [Fact]
    public async Task LoadAsync_TotalReviews_ReflectsServerCount_WhenMoreThanOnePage()
    {
        // The key scenario: server has 75 reviews but we only fetch 50. TotalReviews
        // should show 75 (from TotalCount), not 50 (the visible row count).
        var (vm, repo, _, _, reviews) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(SampleItem(42, "Drill"));
        var items = Enumerable.Range(1, 50).Select(i => SampleReview(i)).ToArray();
        var page = new PagedResult<Review>
        {
            Items = items,
            Page = 1,
            PageSize = 50,
            TotalCount = 75,
        };
        reviews.Setup(r => r.GetForItemAsync(42, 1, 50, default)).ReturnsAsync(page);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Equal(50, vm.Reviews.Count);
        Assert.Equal(75, vm.TotalReviews);
    }

    [Fact]
    public async Task LoadAsync_ReviewsAreReplaced_OnRefresh()
    {
        // Stale reviews from a previous load must be cleared and replaced, not
        // appended to.
        //
        // NOTE: vm.ItemId = 42 triggers a fire-and-forget LoadAsync via
        // OnItemIdChanged, consuming the FIRST sequence slot before the test's
        // explicit awaits run. The sequence therefore needs three entries:
        //   slot 1 → fire-and-forget (ItemId assignment)
        //   slot 2 → first explicit await
        //   slot 3 → second explicit await (the refresh under test)
        var (vm, repo, _, _, reviews) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(SampleItem(42, "Drill"));

        var firstPage = new PagedResult<Review>
        {
            Items = new[] { SampleReview(1), SampleReview(2) },
            TotalCount = 2,
        };
        var secondPage = new PagedResult<Review>
        {
            Items = new[] { SampleReview(3) },
            TotalCount = 1,
        };
        reviews.SetupSequence(r => r.GetForItemAsync(42, 1, 50, default))
               .ReturnsAsync(firstPage)   // slot 1: fire-and-forget from vm.ItemId = 42
               .ReturnsAsync(firstPage)   // slot 2: first explicit LoadAsync
               .ReturnsAsync(secondPage); // slot 3: refresh under test

        vm.ItemId = 42;
        await vm.LoadAsync();
        Assert.Equal(2, vm.Reviews.Count);

        await vm.LoadAsync();
        Assert.Single(vm.Reviews);
        Assert.Equal(1, vm.TotalReviews);
    }

    [Fact]
    public async Task LoadAsync_ClearsReviews_WhenItemNotFound()
    {
        var (vm, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync((Item?)null);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Empty(vm.Reviews);
        Assert.Equal(0, vm.TotalReviews);
    }

    [Fact]
    public async Task LoadAsync_ReviewsAreEmpty_WhenNoReviewsExist()
    {
        var (vm, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(SampleItem(42, "Drill"));
        // Default setup returns empty page — no additional setup needed.

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Empty(vm.Reviews);
        Assert.Equal(0, vm.TotalReviews);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        ItemDetailsViewModel vm,
        Mock<IItemRepository> repo,
        Mock<IAuthenticationService> auth,
        Mock<INavigationService> nav,
        Mock<IReviewRepository> reviews) Build()
    {
        var repo = new Mock<IItemRepository>();
        var reviews = new Mock<IReviewRepository>();
        var auth = new Mock<IAuthenticationService>();
        var nav = new Mock<INavigationService>();

        // Default: return an empty reviews page so tests that don't care about
        // reviews don't need to set up this call explicitly.
        reviews.Setup(r => r.GetForItemAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default))
               .ReturnsAsync(PagedResult<Review>.Empty());

        var vm = new ItemDetailsViewModel(repo.Object, reviews.Object, auth.Object, nav.Object);
        return (vm, repo, auth, nav, reviews);
    }

    private static Review SampleReview(int id) => new()
    {
        Id = id,
        RentalId = id,
        ReviewerId = 99,
        Rating = 4,
        Comment = $"Review {id}",
        ReviewerName = "Zed L.",
        ItemTitle = "Drill",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(id - 1),
    };

    private static Item SampleItem(int id, string title, int ownerId = 7) => new()
    {
        Id = id,
        Title = title,
        Description = "An 18V drill",
        DailyRate = 5.50m,
        CategoryId = 1,
        OwnerId = ownerId,
        Latitude = 0,
        Longitude = 0,
        IsAvailable = true,
        OwnerName = "Ada L.",
        OwnerRating = 4.7,
        AverageRating = 4.5,
        CategoryName = "Power Tools",
    };
}
