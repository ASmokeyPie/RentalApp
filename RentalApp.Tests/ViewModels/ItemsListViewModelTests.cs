using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class ItemsListViewModelTests
{
    // ---- RefreshAsync -----------------------------------------------------

    [Fact]
    public async Task RefreshAsync_LoadsFirstPage_AndPopulatesState()
    {
        var (vm, repo, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .ReturnsAsync(Page(items: new[] { Item(1), Item(2) }, page: 1, pageSize: 20, total: 25));

        await vm.RefreshAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(2, vm.TotalPages);
        Assert.Equal(25, vm.TotalCount);
        Assert.True(vm.HasMorePages);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RefreshAsync_ReplacesExistingItems_OnSecondCall()
    {
        var (vm, repo, _) = Build();
        repo.SetupSequence(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .ReturnsAsync(Page(new[] { Item(1), Item(2) }))
            .ReturnsAsync(Page(new[] { Item(99) }));

        await vm.RefreshAsync();
        await vm.RefreshAsync();

        // Second refresh replaces the collection rather than appending.
        Assert.Single(vm.Items);
        Assert.Equal(99, vm.Items[0].Id);
    }

    [Fact]
    public async Task RefreshAsync_RequestsPage1_WithVmPageSize()
    {
        var (vm, repo, _) = Build();
        vm.PageSize = 5;
        ItemQuery? captured = null;
        repo.Setup(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .Callback<ItemQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(Page(Array.Empty<Item>(), page: 1, pageSize: 5, total: 0));

        await vm.RefreshAsync();

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.Page);
        Assert.Equal(5, captured.PageSize);
    }

    [Fact]
    public async Task RefreshAsync_OnException_SetsErrorAndKeepsRunning()
    {
        var (vm, repo, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .ThrowsAsync(new HttpRequestException("network down"));

        await vm.RefreshAsync();

        Assert.True(vm.HasError);
        Assert.Contains("network down", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task RefreshAsync_OnEmptyResult_SetsIsEmpty()
    {
        var (vm, repo, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .ReturnsAsync(Page(Array.Empty<Item>(), page: 1, pageSize: 20, total: 0));

        await vm.RefreshAsync();

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasMorePages);
    }

    // ---- LoadMoreAsync ----------------------------------------------------

    [Fact]
    public async Task LoadMoreAsync_AppendsNextPage()
    {
        var (vm, repo, _) = Build();
        repo.SetupSequence(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .ReturnsAsync(Page(new[] { Item(1), Item(2) }, page: 1, pageSize: 20, total: 25))
            .ReturnsAsync(Page(new[] { Item(3) }, page: 2, pageSize: 20, total: 25));

        await vm.RefreshAsync();
        await vm.LoadMoreAsync();

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(new[] { 1, 2, 3 }, vm.Items.Select(i => i.Id));
        Assert.Equal(2, vm.CurrentPage);
    }

    [Fact]
    public async Task LoadMoreAsync_NoOps_WhenNoMorePages()
    {
        var (vm, repo, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<ItemQuery>(), default))
            .ReturnsAsync(Page(new[] { Item(1) }, page: 1, pageSize: 20, total: 1));

        await vm.RefreshAsync();
        await vm.LoadMoreAsync();

        // SearchAsync called exactly once — LoadMoreAsync recognised "we're already on the last page".
        repo.Verify(r => r.SearchAsync(It.IsAny<ItemQuery>(), default), Times.Once);
    }

    [Fact]
    public async Task LoadMoreAsync_NoOps_BeforeFirstLoad()
    {
        var (vm, repo, _) = Build();

        await vm.LoadMoreAsync();

        // No first page yet — HasMorePages is false, so LoadMore must not call.
        repo.Verify(r => r.SearchAsync(It.IsAny<ItemQuery>(), default), Times.Never);
    }

    // ---- SelectItemAsync --------------------------------------------------

    [Fact]
    public async Task SelectItemAsync_NavigatesToDetailPage_WithItemId()
    {
        var (vm, _, nav) = Build();

        await vm.SelectItemAsync(Item(42));

        nav.Verify(n => n.NavigateToAsync(
                "ItemDetailsPage",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("itemId") && (int)d["itemId"] == 42)),
            Times.Once);
    }

    [Fact]
    public async Task SelectItemAsync_TolerantOfNull()
    {
        var (vm, _, nav) = Build();

        await vm.SelectItemAsync(null);

        nav.Verify(n => n.NavigateToAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
            Times.Never);
    }

    // ---- NavigateToCreateItemAsync ----------------------------------------

    [Fact]
    public async Task NavigateToCreateItemAsync_HitsCreateItemRoute()
    {
        var (vm, _, nav) = Build();

        await vm.NavigateToCreateItemAsync();

        nav.Verify(n => n.NavigateToAsync("CreateItemPage"), Times.Once);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (ItemsListViewModel vm, Mock<IItemRepository> repo, Mock<INavigationService> nav) Build()
    {
        var repo = new Mock<IItemRepository>();
        var nav = new Mock<INavigationService>();
        var vm = new ItemsListViewModel(repo.Object, nav.Object);
        return (vm, repo, nav);
    }

    private static Item Item(int id) => new()
    {
        Id = id,
        Title = $"Item {id}",
        DailyRate = 5m,
        CategoryId = 1,
        OwnerId = 1,
        Latitude = 0,
        Longitude = 0,
    };

    private static PagedResult<Item> Page(
        IReadOnlyList<Item> items, int page = 1, int pageSize = 20, int? total = null) =>
        new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total ?? items.Count,
        };
}
