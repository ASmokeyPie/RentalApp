using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class EditItemViewModelTests
{
    // ---- LoadAsync --------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ProceedsEvenWhenIsRefreshingAlreadyTrue()
    {
        // Regression: RefreshView toggles IsRefreshing=true BEFORE firing the
        // command. An early-return on IsRefreshing would leave the spinner
        // stuck. LoadAsync must still run and clear the flag in finally.
        // Arrange
        var (vm, items, auth, _) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.IsRefreshing = true;
        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.False(vm.IsRefreshing);
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task LoadAsync_PopulatesForm_AndSetsCanEdit_WhenOwner()
    {
        // Arrange
        var (vm, items, auth, _) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7, dailyRate: 8.50m));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.True(vm.CanEdit);
        Assert.Equal("Drill", vm.ItemTitle);
        Assert.Equal("8.5", vm.DailyRateText);
        Assert.True(vm.IsAvailable);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_LocksForm_WhenCurrentUserIsNotOwner()
    {
        // Arrange
        var (vm, items, auth, _) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.False(vm.CanEdit);
        Assert.True(vm.HasError);
        Assert.Contains("only edit items you own", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_LocksForm_WhenItemNotFound()
    {
        // Arrange
        var (vm, items, _, _) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync((Item?)null);

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.False(vm.CanEdit);
        Assert.True(vm.HasError);
        Assert.Contains("could not be found", vm.ErrorMessage);
    }

    // ---- SubmitAsync ------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_PutsUpdatedItem_AndNavigatesBack_OnSuccess()
    {
        // Arrange
        var (vm, items, auth, nav) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });
        items.Setup(i => i.UpdateAsync(It.IsAny<Item>(), default))
             .ReturnsAsync((Item entity, CancellationToken _) => entity);

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();

        // User edits a couple of fields and saves.
        vm.ItemTitle = "Renamed Drill";
        vm.DailyRateText = "15.00";
        vm.IsAvailable = false;

        await vm.SubmitAsync();

        // Assert
        items.Verify(i => i.UpdateAsync(
                It.Is<Item>(it => it.Id == 42 && it.Title == "Renamed Drill" && it.DailyRate == 15.00m && !it.IsAvailable),
                default),
            Times.Once);
        nav.Verify(n => n.NavigateBackAsync(), Times.Once);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_DoesNothing_WhenNotOwner()
    {
        // Arrange
        var (vm, items, auth, nav) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 99, Email = "z@b.c", FirstName = "Zed", LastName = "L" });

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();
        await vm.SubmitAsync();

        // Assert
        items.Verify(i => i.UpdateAsync(It.IsAny<Item>(), default), Times.Never);
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_DoesNotNavigate_WhenValidationFails()
    {
        // Arrange
        var (vm, items, auth, nav) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();

        // Force validation failure.
        vm.ItemTitle = "no";

        await vm.SubmitAsync();

        // Assert
        items.Verify(i => i.UpdateAsync(It.IsAny<Item>(), default), Times.Never);
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_OnRepoException_SetsError_AndDoesNotNavigate()
    {
        // Arrange
        var (vm, items, auth, nav) = Build();
        items.Setup(i => i.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, ownerId: 7));
        auth.SetupGet(a => a.CurrentUser).Returns(new User { Id = 7, Email = "a@b.c", FirstName = "Ada", LastName = "L" });
        items.Setup(i => i.UpdateAsync(It.IsAny<Item>(), default))
             .ThrowsAsync(new HttpRequestException("network down"));

        vm.ItemId = 42;

        // Act
        await vm.LoadAsync();
        await vm.SubmitAsync();

        // Assert
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("network down", vm.ErrorMessage);
    }

    // ---- TryBuildItem (validation rules same as Create — sample one) -----

    [Fact]
    public void TryBuildItem_PreservesNonEditableFields()
    {
        // Arrange
        var (vm, _, _, _) = Build();
        var loaded = SampleItem(42, ownerId: 7, dailyRate: 5m);
        loaded.CategoryId = 8;
        loaded.Latitude = 55.95;
        loaded.Longitude = -3.19;

        // Skip the load round-trip; set OriginalItem directly so we can assert the
        // builder preserves CategoryId/lat/lon/OwnerId from the loaded original.
        vm.OriginalItem = loaded;
        vm.CanEdit = true;
        vm.ItemTitle = "Renamed Drill";
        vm.Description = "fresh desc";
        vm.DailyRateText = "20";
        vm.IsAvailable = false;

        // Act
        var ok = vm.TryBuildItem(out var item, out var err);

        // Assert
        Assert.True(ok);
        Assert.Equal(string.Empty, err);
        Assert.Equal(8, item.CategoryId);
        Assert.Equal(7, item.OwnerId);
        Assert.Equal(55.95, item.Latitude);
        Assert.Equal(-3.19, item.Longitude);
        Assert.Equal(20m, item.DailyRate);
        Assert.False(item.IsAvailable);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        EditItemViewModel vm,
        Mock<IItemRepository> items,
        Mock<IAuthenticationService> auth,
        Mock<INavigationService> navigation) Build()
    {
        var items = new Mock<IItemRepository>();
        var auth = new Mock<IAuthenticationService>();
        var nav = new Mock<INavigationService>();
        var vm = new EditItemViewModel(items.Object, auth.Object, nav.Object);
        return (vm, items, auth, nav);
    }

    private static Item SampleItem(int id, int ownerId, decimal dailyRate = 5m) => new()
    {
        Id = id,
        Title = "Drill",
        Description = "An 18V drill",
        DailyRate = dailyRate,
        CategoryId = 1,
        OwnerId = ownerId,
        Latitude = 55.95,
        Longitude = -3.19,
        IsAvailable = true,
        CategoryName = "Power Tools",
    };
}
