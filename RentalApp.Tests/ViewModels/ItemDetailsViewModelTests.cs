using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class ItemDetailsViewModelTests
{
    [Fact]
    public async Task LoadAsync_NoOps_WhenItemIdIsZero()
    {
        var (vm, repo, _, _) = Build();

        await vm.LoadAsync();

        repo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
        Assert.False(vm.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_PopulatesItem_OnSuccess_AndUpdatesTitle()
    {
        var (vm, repo, _, _) = Build();
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
        var (vm, repo, _, _) = Build();
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
        var (vm, repo, _, _) = Build();
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
        var (vm, repo, _, _) = Build();
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
        var (vm, repo, auth, _) = Build();
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
        var (vm, repo, auth, _) = Build();
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
        var (vm, repo, auth, _) = Build();
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
        var (vm, repo, auth, nav) = Build();
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
        var (vm, repo, auth, nav) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(SampleItem(42, "Drill", ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;
        await vm.LoadAsync();
        await vm.EditItemAsync();

        nav.Verify(n => n.NavigateToAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
            Times.Never);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        ItemDetailsViewModel vm,
        Mock<IItemRepository> repo,
        Mock<IAuthenticationService> auth,
        Mock<INavigationService> nav) Build()
    {
        var repo = new Mock<IItemRepository>();
        var auth = new Mock<IAuthenticationService>();
        var nav = new Mock<INavigationService>();
        var vm = new ItemDetailsViewModel(repo.Object, auth.Object, nav.Object);
        return (vm, repo, auth, nav);
    }

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
