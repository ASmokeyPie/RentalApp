using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class FindNearbyViewModelTests
{
    // ---- LoadCategoriesAsync ---------------------------------------------

    [Fact]
    public async Task LoadCategoriesAsync_PopulatesPicker_WithSyntheticAllOptionFirst()
    {
        var (vm, _, _, cats, _) = Build();
        cats.Setup(c => c.ListAsync(default))
            .ReturnsAsync(new List<Category>
            {
                new() { Id = 1, Name = "Power Tools",  Slug = "power-tools" },
                new() { Id = 2, Name = "Camping Gear", Slug = "camping-gear" },
            });

        await vm.LoadCategoriesAsync();

        // Three entries: synthetic "All" + the two from the API.
        Assert.Equal(3, vm.Categories.Count);
        Assert.Equal("All categories", vm.Categories[0].Name);
        Assert.Equal("Power Tools",   vm.Categories[1].Name);
    }

    // ---- RefreshAsync ----------------------------------------------------

    [Fact]
    public async Task RefreshAsync_ReadsGps_ThenQueriesNearby()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));
        items.Setup(i => i.GetNearbyAsync(55.95, -3.19, It.IsAny<double>(), null, default))
             .ReturnsAsync(new[] { Item(1, distance: 0.5) });

        await vm.RefreshAsync();

        Assert.Single(vm.Items);
        Assert.Equal(55.95, vm.SearchLatitude);
        Assert.Equal(-3.19, vm.SearchLongitude);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RefreshAsync_PassesRadiusToRepo()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));
        double? capturedRadius = null;
        items.Setup(i => i.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), null, default))
             .Callback<double, double, double, string?, CancellationToken>((_, _, r, _, _) => capturedRadius = r)
             .ReturnsAsync(Array.Empty<Item>());

        vm.RadiusKm = 12;
        await vm.RefreshAsync();

        Assert.Equal(12, capturedRadius);
    }

    [Fact]
    public async Task RefreshAsync_OmitsCategorySlug_WhenAllOptionSelected()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));
        string? capturedSlug = "(unset)";
        items.Setup(i => i.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), default))
             .Callback<double, double, double, string?, CancellationToken>((_, _, _, s, _) => capturedSlug = s)
             .ReturnsAsync(Array.Empty<Item>());

        // SelectedCategory defaults to AllCategoriesOption (Id=0).
        await vm.RefreshAsync();

        Assert.Null(capturedSlug);
    }

    [Fact]
    public async Task RefreshAsync_PassesCategorySlug_WhenRealCategoryPicked()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));
        string? capturedSlug = null;
        items.Setup(i => i.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), default))
             .Callback<double, double, double, string?, CancellationToken>((_, _, _, s, _) => capturedSlug = s)
             .ReturnsAsync(Array.Empty<Item>());

        vm.SelectedCategory = new Category { Id = 1, Name = "Power Tools", Slug = "power-tools" };
        await vm.RefreshAsync();

        Assert.Equal("power-tools", capturedSlug);
    }

    [Fact]
    public async Task RefreshAsync_OnPermissionDenied_SetsError_AndDoesNotQuery()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((((double, double)?)null));

        await vm.RefreshAsync();

        items.Verify(i => i.GetNearbyAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), default),
            Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("location", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_OnRepoException_SetsError_AndClearsSpinner()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));
        items.Setup(i => i.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), null, default))
             .ThrowsAsync(new HttpRequestException("server down"));

        await vm.RefreshAsync();

        Assert.True(vm.HasError);
        Assert.Contains("server down", vm.ErrorMessage);
        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public async Task RefreshAsync_OnEmptyResult_SetsIsEmpty()
    {
        var (vm, location, items, _, _) = Build();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));
        items.Setup(i => i.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), null, default))
             .ReturnsAsync(Array.Empty<Item>());

        await vm.RefreshAsync();

        Assert.True(vm.IsEmpty);
    }

    // ---- SelectItemAsync -------------------------------------------------

    [Fact]
    public async Task SelectItemAsync_NavigatesToItemDetail()
    {
        var (vm, _, _, _, nav) = Build();

        await vm.SelectItemAsync(Item(42, distance: 1.2));

        nav.Verify(n => n.NavigateToAsync(
                "ItemDetailsPage",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("itemId") && (int)d["itemId"] == 42)),
            Times.Once);
    }

    // ---- Helpers ---------------------------------------------------------

    private static (
        FindNearbyViewModel vm,
        Mock<ILocationService> location,
        Mock<IItemRepository> items,
        Mock<ICategoryRepository> categories,
        Mock<INavigationService> navigation) Build()
    {
        var location = new Mock<ILocationService>();
        var items = new Mock<IItemRepository>();
        var cats = new Mock<ICategoryRepository>();
        var nav = new Mock<INavigationService>();
        var vm = new FindNearbyViewModel(location.Object, items.Object, cats.Object, nav.Object);
        return (vm, location, items, cats, nav);
    }

    private static Item Item(int id, double distance) => new()
    {
        Id = id,
        Title = $"Item {id}",
        DailyRate = 5m,
        CategoryId = 1,
        OwnerId = 1,
        Latitude = 55.95,
        Longitude = -3.19,
        DistanceKm = distance,
    };
}
