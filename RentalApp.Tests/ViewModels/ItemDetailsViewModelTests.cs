using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class ItemDetailsViewModelTests
{
    [Fact]
    public async Task LoadAsync_NoOps_WhenItemIdIsZero()
    {
        var (vm, repo) = Build();

        await vm.LoadAsync();

        repo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
        Assert.False(vm.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_PopulatesItem_OnSuccess_AndUpdatesTitle()
    {
        var (vm, repo) = Build();
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
        var (vm, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync((Item?)null);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Null(vm.Item);
        Assert.False(vm.IsLoaded);
        Assert.True(vm.HasError);
        Assert.Contains("could not be found", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_OnException_SetsError_AndIsLoadedStaysFalse()
    {
        var (vm, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(7, default))
            .ThrowsAsync(new HttpRequestException("network down"));

        vm.ItemId = 7;
        await vm.LoadAsync();

        Assert.Null(vm.Item);
        Assert.False(vm.IsLoaded);
        Assert.True(vm.HasError);
        Assert.Contains("network down", vm.ErrorMessage);
    }

    [Fact]
    public async Task SettingItemId_TriggersAutomaticLoad()
    {
        // OnItemIdChanged fires LoadAsync as fire-and-forget. We can't await
        // it directly, so verify the side-effect by polling briefly.
        var (vm, repo) = Build();
        var tcs = new TaskCompletionSource<Item?>();
        repo.Setup(r => r.GetByIdAsync(42, default)).Returns(tcs.Task);

        vm.ItemId = 42;

        // Repo invocation happened synchronously (LoadAsync ran up to the await).
        repo.Verify(r => r.GetByIdAsync(42, default), Times.Once);

        // Complete the load so we leave the test in a clean state.
        tcs.SetResult(SampleItem(42, "Drill"));
        // Give the continuation a tick to flush.
        await Task.Yield();
    }

    // ---- Helpers ----------------------------------------------------------

    private static (ItemDetailsViewModel vm, Mock<IItemRepository> repo) Build()
    {
        var repo = new Mock<IItemRepository>();
        var vm = new ItemDetailsViewModel(repo.Object);
        return (vm, repo);
    }

    private static Item SampleItem(int id, string title) => new()
    {
        Id = id,
        Title = title,
        Description = "An 18V drill",
        DailyRate = 5.50m,
        CategoryId = 1,
        OwnerId = 7,
        Latitude = 0,
        Longitude = 0,
        IsAvailable = true,
        OwnerName = "Ada L.",
        OwnerRating = 4.7,
        AverageRating = 4.5,
        CategoryName = "Power Tools",
    };
}
