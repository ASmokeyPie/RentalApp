using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class CreateItemViewModelTests
{
    // ---- LoadCategoriesAsync ---------------------------------------------

    [Fact]
    public async Task LoadCategoriesAsync_PopulatesCollection()
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
        Assert.Equal(2, vm.Categories.Count);
        Assert.Equal("Power Tools", vm.Categories[0].Name);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadCategoriesAsync_OnException_SetsError()
    {
        // Arrange
        var (vm, _, cats, _) = Build();
        cats.Setup(c => c.ListAsync(default))
            .ThrowsAsync(new HttpRequestException("offline"));

        // Act
        await vm.LoadCategoriesAsync();

        // Assert
        Assert.True(vm.HasError);
        Assert.Empty(vm.Categories);
    }

    // ---- TryBuildItem (validation) ---------------------------------------

    [Fact]
    public void TryBuildItem_ReturnsItem_WhenAllFieldsValid()
    {
        // Arrange
        var (vm, _, _, _) = Build();
        vm.SelectedCategory = new Category { Id = 3, Name = "X", Slug = "x" };
        vm.ItemTitle = "Drill 18V";
        vm.Description = "An 18V cordless drill";
        vm.DailyRateText = "12.50";
        vm.LatitudeText = "55.95";
        vm.LongitudeText = "-3.19";

        // Act
        var ok = vm.TryBuildItem(out var item, out var err);

        // Assert
        Assert.True(ok);
        Assert.Equal(string.Empty, err);
        Assert.Equal("Drill 18V", item.Title);
        Assert.Equal(12.50m, item.DailyRate);
        Assert.Equal(3, item.CategoryId);
        Assert.Equal(55.95, item.Latitude);
        Assert.Equal(-3.19, item.Longitude);
    }

    [Theory]
    [InlineData("",       "Title")]   // empty
    [InlineData("xx",     "Title")]   // too short
    public void TryBuildItem_RejectsBadTitle(string title, string expected)
    {
        // Arrange
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.ItemTitle = title;

        // Act
        var ok = vm.TryBuildItem(out _, out var err);

        // Assert
        Assert.False(ok);
        Assert.Contains(expected, err);
    }

    [Theory]
    [InlineData("",     "rate")]    // unparseable empty
    [InlineData("0",    "rate")]    // not > 0
    [InlineData("9999", "rate")]    // exceeds 1000 cap
    [InlineData("abc",  "rate")]    // unparseable
    public void TryBuildItem_RejectsBadDailyRate(string value, string expected)
    {
        // Arrange
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.DailyRateText = value;

        // Act
        var ok = vm.TryBuildItem(out _, out var err);

        // Assert
        Assert.False(ok);
        Assert.Contains(expected, err);
    }

    [Fact]
    public void TryBuildItem_RejectsMissingCategory()
    {
        // Arrange
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.SelectedCategory = null;

        // Act
        var ok = vm.TryBuildItem(out _, out var err);

        // Assert
        Assert.False(ok);
        Assert.Contains("category", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("100", "Latitude")]  // out of range
    [InlineData("",    "Latitude")]  // empty
    public void TryBuildItem_RejectsBadLatitude(string value, string expected)
    {
        // Arrange
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.LatitudeText = value;

        // Act
        var ok = vm.TryBuildItem(out _, out var err);

        // Assert
        Assert.False(ok);
        Assert.Contains(expected, err);
    }

    // ---- SubmitAsync ------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_PostsItem_AndNavigatesBack_OnSuccess()
    {
        // Arrange
        var (vm, items, _, nav) = Build();
        SetValidDefaults(vm);
        items.Setup(i => i.CreateAsync(It.IsAny<Item>(), default))
             .ReturnsAsync((Item entity, CancellationToken _) => entity);

        // Act
        await vm.SubmitAsync();

        // Assert
        items.Verify(i => i.CreateAsync(
                It.Is<Item>(it => it.Title == "Drill 18V" && it.DailyRate == 12.50m),
                default),
            Times.Once);
        nav.Verify(n => n.NavigateBackAsync(), Times.Once);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_DoesNotNavigate_WhenValidationFails()
    {
        // Arrange
        var (vm, items, _, nav) = Build();
        // Leave fields empty so validation rejects.

        // Act
        await vm.SubmitAsync();

        // Assert
        items.Verify(i => i.CreateAsync(It.IsAny<Item>(), default), Times.Never);
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_OnRepoException_SetsError_AndDoesNotNavigate()
    {
        // Arrange
        var (vm, items, _, nav) = Build();
        SetValidDefaults(vm);
        items.Setup(i => i.CreateAsync(It.IsAny<Item>(), default))
             .ThrowsAsync(new HttpRequestException("server fire"));

        // Act
        await vm.SubmitAsync();

        // Assert
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("server fire", vm.ErrorMessage);
    }

    // ---- UseCurrentLocationAsync -----------------------------------------

    [Fact]
    public async Task UseCurrentLocationAsync_FillsLatLon_OnSuccess()
    {
        // Arrange
        var (vm, _, _, _, location) = BuildAll();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((55.95, -3.19));

        // Act
        await vm.UseCurrentLocationAsync();

        // Assert
        Assert.Equal("55.95", vm.LatitudeText);
        Assert.Equal("-3.19", vm.LongitudeText);
        Assert.False(vm.HasError);
        Assert.False(vm.IsFetchingLocation);
    }

    [Fact]
    public async Task UseCurrentLocationAsync_OnPermissionDenied_SetsError_AndLeavesFieldsBlank()
    {
        // Arrange
        var (vm, _, _, _, location) = BuildAll();
        location.Setup(l => l.GetCurrentLocationAsync(default))
                .ReturnsAsync((((double, double)?)null));

        // Act
        await vm.UseCurrentLocationAsync();

        // Assert
        Assert.Equal(string.Empty, vm.LatitudeText);
        Assert.Equal(string.Empty, vm.LongitudeText);
        Assert.True(vm.HasError);
        Assert.Contains("location", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsFetchingLocation);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        CreateItemViewModel vm,
        Mock<IItemRepository> items,
        Mock<ICategoryRepository> categories,
        Mock<INavigationService> navigation) Build()
    {
        var (vm, items, cats, nav, _) = BuildAll();
        return (vm, items, cats, nav);
    }

    private static (
        CreateItemViewModel vm,
        Mock<IItemRepository> items,
        Mock<ICategoryRepository> categories,
        Mock<INavigationService> navigation,
        Mock<ILocationService> location) BuildAll()
    {
        var items = new Mock<IItemRepository>();
        var cats = new Mock<ICategoryRepository>();
        var nav = new Mock<INavigationService>();
        var location = new Mock<ILocationService>();
        var vm = new CreateItemViewModel(items.Object, cats.Object, nav.Object, location.Object);
        return (vm, items, cats, nav, location);
    }

    private static void SetValidDefaults(CreateItemViewModel vm)
    {
        vm.SelectedCategory = new Category { Id = 3, Name = "X", Slug = "x" };
        vm.ItemTitle = "Drill 18V";
        vm.Description = "An 18V cordless drill";
        vm.DailyRateText = "12.50";
        vm.LatitudeText = "55.95";
        vm.LongitudeText = "-3.19";
    }
}
