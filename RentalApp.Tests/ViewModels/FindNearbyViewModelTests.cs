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
        // Arrange
        var (vm, _, cats, _) = Build();
        cats.Setup(c => c.ListAsync(default))
            .ReturnsAsync(new List<Category>
            {
                new() { Id = 1, Name = "Power Tools",  Slug = "power-tools" },
                new() { Id = 2, Name = "Camping Gear", Slug = "camping-gear" },
            });

        // Act
        await vm.LoadCategoriesAsync();

        // Assert
        // Three entries: synthetic "All" + the two from the API.
        Assert.Equal(3, vm.Categories.Count);
        Assert.Equal("All categories", vm.Categories[0].Name);
        Assert.Equal("Power Tools",   vm.Categories[1].Name);
    }

    // ---- RefreshAsync ----------------------------------------------------

    [Fact]
    public async Task RefreshAsync_DelegatesToLocationService_AndPopulatesItems()
    {
        // Arrange
        var (vm, location, _, _) = Build();
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), null, default))
                .ReturnsAsync(new NearbySearchResult(55.95, -3.19, new[] { Item(1, distance: 0.5) }));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Single(vm.Items);
        Assert.Equal(55.95, vm.SearchLatitude);
        Assert.Equal(-3.19, vm.SearchLongitude);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RefreshAsync_PassesRadiusToService()
    {
        // Arrange
        var (vm, location, _, _) = Build();
        double? capturedRadius = null;
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), It.IsAny<string?>(), default))
                .Callback<double, string?, CancellationToken>((r, _, _) => capturedRadius = r)
                .ReturnsAsync(new NearbySearchResult(0, 0, Array.Empty<Item>()));

        vm.RadiusKm = 12;

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Equal(12, capturedRadius);
    }

    [Fact]
    public async Task RefreshAsync_OmitsCategorySlug_WhenAllOptionSelected()
    {
        // Arrange
        var (vm, location, _, _) = Build();
        string? capturedSlug = "(unset)";
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), It.IsAny<string?>(), default))
                .Callback<double, string?, CancellationToken>((_, s, _) => capturedSlug = s)
                .ReturnsAsync(new NearbySearchResult(0, 0, Array.Empty<Item>()));

        // SelectedCategory defaults to AllCategoriesOption (Id=0).

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Null(capturedSlug);
    }

    [Fact]
    public async Task RefreshAsync_PassesCategorySlug_WhenRealCategoryPicked()
    {
        // Arrange
        var (vm, location, _, _) = Build();
        string? capturedSlug = null;
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), It.IsAny<string?>(), default))
                .Callback<double, string?, CancellationToken>((_, s, _) => capturedSlug = s)
                .ReturnsAsync(new NearbySearchResult(0, 0, Array.Empty<Item>()));

        vm.SelectedCategory = new Category { Id = 1, Name = "Power Tools", Slug = "power-tools" };

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Equal("power-tools", capturedSlug);
    }

    [Fact]
    public async Task RefreshAsync_OnNullResult_SetsError()
    {
        // A null result from the service means the GPS read failed (no
        // permission / no fix). VM surfaces a friendly error.
        // Arrange
        var (vm, location, _, _) = Build();
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), It.IsAny<string?>(), default))
                .ReturnsAsync((NearbySearchResult?)null);

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.HasError);
        Assert.Contains("location", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_OnException_SetsError_AndClearsSpinner()
    {
        // Arrange
        var (vm, location, _, _) = Build();
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), It.IsAny<string?>(), default))
                .ThrowsAsync(new HttpRequestException("server down"));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.HasError);
        Assert.Contains("server down", vm.ErrorMessage);
        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public async Task RefreshAsync_OnEmptyResult_SetsIsEmpty()
    {
        // Arrange
        var (vm, location, _, _) = Build();
        location.Setup(l => l.FindNearbyItemsAsync(It.IsAny<double>(), It.IsAny<string?>(), default))
                .ReturnsAsync(new NearbySearchResult(55.95, -3.19, Array.Empty<Item>()));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.IsEmpty);
    }

    // ---- SelectItemAsync -------------------------------------------------

    [Fact]
    public async Task SelectItemAsync_NavigatesToItemDetail()
    {
        // Arrange
        var (vm, _, _, nav) = Build();

        // Act
        await vm.SelectItemAsync(Item(42, distance: 1.2));

        // Assert
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
        Mock<ICategoryRepository> categories,
        Mock<INavigationService> navigation) Build()
    {
        var location = new Mock<ILocationService>();
        var cats = new Mock<ICategoryRepository>();
        var nav = new Mock<INavigationService>();
        var vm = new FindNearbyViewModel(location.Object, cats.Object, nav.Object);
        return (vm, location, cats, nav);
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
