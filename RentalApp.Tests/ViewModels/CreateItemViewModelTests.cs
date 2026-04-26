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
        var (vm, _, cats, _) = Build();
        cats.Setup(c => c.ListAsync(default))
            .ReturnsAsync(new List<Category>
            {
                new() { Id = 1, Name = "Power Tools",  Slug = "power-tools" },
                new() { Id = 2, Name = "Camping Gear", Slug = "camping-gear" },
            });

        await vm.LoadCategoriesAsync();

        Assert.Equal(2, vm.Categories.Count);
        Assert.Equal("Power Tools", vm.Categories[0].Name);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadCategoriesAsync_OnException_SetsError()
    {
        var (vm, _, cats, _) = Build();
        cats.Setup(c => c.ListAsync(default))
            .ThrowsAsync(new HttpRequestException("offline"));

        await vm.LoadCategoriesAsync();

        Assert.True(vm.HasError);
        Assert.Empty(vm.Categories);
    }

    // ---- TryBuildItem (validation) ---------------------------------------

    [Fact]
    public void TryBuildItem_ReturnsItem_WhenAllFieldsValid()
    {
        var (vm, _, _, _) = Build();
        vm.SelectedCategory = new Category { Id = 3, Name = "X", Slug = "x" };
        vm.ItemTitle = "Drill 18V";
        vm.Description = "An 18V cordless drill";
        vm.DailyRateText = "12.50";
        vm.LatitudeText = "55.95";
        vm.LongitudeText = "-3.19";

        var ok = vm.TryBuildItem(out var item, out var err);

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
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.ItemTitle = title;

        var ok = vm.TryBuildItem(out _, out var err);

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
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.DailyRateText = value;

        var ok = vm.TryBuildItem(out _, out var err);

        Assert.False(ok);
        Assert.Contains(expected, err);
    }

    [Fact]
    public void TryBuildItem_RejectsMissingCategory()
    {
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.SelectedCategory = null;

        var ok = vm.TryBuildItem(out _, out var err);

        Assert.False(ok);
        Assert.Contains("category", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("100", "Latitude")]  // out of range
    [InlineData("",    "Latitude")]  // empty
    public void TryBuildItem_RejectsBadLatitude(string value, string expected)
    {
        var (vm, _, _, _) = Build();
        SetValidDefaults(vm);
        vm.LatitudeText = value;

        var ok = vm.TryBuildItem(out _, out var err);

        Assert.False(ok);
        Assert.Contains(expected, err);
    }

    // ---- SubmitAsync ------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_PostsItem_AndNavigatesBack_OnSuccess()
    {
        var (vm, items, _, nav) = Build();
        SetValidDefaults(vm);
        items.Setup(i => i.CreateAsync(It.IsAny<Item>(), default))
             .ReturnsAsync((Item entity, CancellationToken _) => entity);

        await vm.SubmitAsync();

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
        var (vm, items, _, nav) = Build();
        // Leave fields empty so validation rejects.

        await vm.SubmitAsync();

        items.Verify(i => i.CreateAsync(It.IsAny<Item>(), default), Times.Never);
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_OnRepoException_SetsError_AndDoesNotNavigate()
    {
        var (vm, items, _, nav) = Build();
        SetValidDefaults(vm);
        items.Setup(i => i.CreateAsync(It.IsAny<Item>(), default))
             .ThrowsAsync(new HttpRequestException("server fire"));

        await vm.SubmitAsync();

        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("server fire", vm.ErrorMessage);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        CreateItemViewModel vm,
        Mock<IItemRepository> items,
        Mock<ICategoryRepository> categories,
        Mock<INavigationService> navigation) Build()
    {
        var items = new Mock<IItemRepository>();
        var cats = new Mock<ICategoryRepository>();
        var nav = new Mock<INavigationService>();
        var vm = new CreateItemViewModel(items.Object, cats.Object, nav.Object);
        return (vm, items, cats, nav);
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
