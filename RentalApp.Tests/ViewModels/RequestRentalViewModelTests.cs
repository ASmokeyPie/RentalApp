using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class RequestRentalViewModelTests
{
    // ---- LoadAsync --------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ProceedsEvenWhenIsRefreshingAlreadyTrue()
    {
        // Regression: RefreshView toggles IsRefreshing=true BEFORE firing the
        // command. An early-return on IsRefreshing would leave the spinner
        // stuck. LoadAsync must still run and clear the flag in finally.
        var (vm, items, _, _) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));

        vm.IsRefreshing = true;
        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.False(vm.IsRefreshing);
        Assert.True(vm.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_PopulatesItem_AndUpdatesTitle_OnSuccess()
    {
        var (vm, items, _, _) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.NotNull(vm.Item);
        Assert.Equal(42, vm.Item!.Id);
        Assert.True(vm.IsLoaded);
        Assert.False(vm.HasError);
        Assert.Equal("Rent: Drill", vm.Title);
    }

    [Fact]
    public async Task LoadAsync_OnNotFound_SetsError_AndIsLoadedFalse()
    {
        var (vm, items, _, _) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync((Item?)null);

        vm.ItemId = 42;
        await vm.LoadAsync();

        Assert.Null(vm.Item);
        Assert.False(vm.IsLoaded);
        Assert.True(vm.HasError);
    }

    // ---- TotalPrice / TotalDays (live updates) ---------------------------

    [Fact]
    public async Task TotalPrice_UpdatesWhenDatesChange()
    {
        var (vm, items, _, _) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));
        vm.ItemId = 42;
        await vm.LoadAsync();

        vm.StartDate = DateTime.Today.AddDays(1);
        vm.EndDate   = DateTime.Today.AddDays(3);

        // 3 inclusive days at £5 = £15
        Assert.Equal(3, vm.TotalDays);
        Assert.Equal(15m, vm.TotalPrice);
    }

    [Fact]
    public async Task TotalPrice_ZeroBeforeItemLoads()
    {
        var (vm, _, _, _) = Build();
        vm.StartDate = DateTime.Today.AddDays(1);
        vm.EndDate   = DateTime.Today.AddDays(3);

        await Task.Yield();

        Assert.Equal(0m, vm.TotalPrice);
    }

    [Fact]
    public async Task StartDate_DragsEndDateForward_WhenStartMovesPastEnd()
    {
        var (vm, items, _, _) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));
        vm.ItemId = 42;
        await vm.LoadAsync();

        vm.StartDate = DateTime.Today.AddDays(1);
        vm.EndDate   = DateTime.Today.AddDays(2);
        // Now move start past current end:
        vm.StartDate = DateTime.Today.AddDays(5);

        Assert.Equal(DateTime.Today.AddDays(5), vm.EndDate);
        Assert.Equal(1, vm.TotalDays);
    }

    // ---- SubmitAsync ------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_DelegatesToService_AndNavigatesBack_OnSuccess()
    {
        var (vm, items, rentals, nav) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));
        rentals.Setup(s => s.RequestRentalAsync(42, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
               .ReturnsAsync(new Rental { Id = 99 });

        vm.ItemId = 42;
        await vm.LoadAsync();
        vm.StartDate = DateTime.Today.AddDays(1);
        vm.EndDate   = DateTime.Today.AddDays(3);

        await vm.SubmitAsync();

        rentals.Verify(s => s.RequestRentalAsync(
                42,
                DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                default),
            Times.Once);
        nav.Verify(n => n.NavigateBackAsync(), Times.Once);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_DoesNothing_WhenItemNotLoaded()
    {
        var (vm, _, rentals, nav) = Build();

        await vm.SubmitAsync();

        rentals.Verify(s => s.RequestRentalAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default),
            Times.Never);
        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SubmitAsync_SurfacesServiceValidationErrors()
    {
        // Service throws InvalidOperationException for past start dates etc.
        var (vm, items, rentals, nav) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));
        rentals.Setup(s => s.RequestRentalAsync(42, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
               .ThrowsAsync(new InvalidOperationException("Start date cannot be in the past."));

        vm.ItemId = 42;
        await vm.LoadAsync();

        await vm.SubmitAsync();

        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("past", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_SurfacesServerError_LikeConflict()
    {
        // Repo translates an HTTP 409 response into HttpRequestException —
        // the VM should surface the message via the error banner without
        // navigating away.
        var (vm, items, rentals, nav) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));
        rentals.Setup(s => s.RequestRentalAsync(42, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
               .ThrowsAsync(new HttpRequestException("Item is already booked for these dates."));

        vm.ItemId = 42;
        await vm.LoadAsync();

        await vm.SubmitAsync();

        nav.Verify(n => n.NavigateBackAsync(), Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("already booked", vm.ErrorMessage);
    }

    [Fact]
    public async Task SubmitAsync_RejectsEndBeforeStart_LocalGuard()
    {
        var (vm, items, rentals, _) = Build();
        items.Setup(r => r.GetByIdAsync(42, default))
             .ReturnsAsync(SampleItem(42, "Drill", dailyRate: 5m));
        vm.ItemId = 42;
        await vm.LoadAsync();

        // Bypass the auto-correct in OnStartDateChanged by setting EndDate
        // first, then forcing StartDate to a value past it via reflection-style
        // direct assignment. Actually simpler: just set a weird state by
        // setting EndDate < StartDate after load.
        // (StartDate setter would drag EndDate; flip the other way.)
        vm.StartDate = DateTime.Today.AddDays(5);
        // OnStartDateChanged dragged EndDate to today+5. Now set EndDate
        // back to before StartDate to simulate an invalid state.
        vm.EndDate = DateTime.Today.AddDays(1);

        await vm.SubmitAsync();

        rentals.Verify(s => s.RequestRentalAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default),
            Times.Never);
        Assert.True(vm.HasError);
        Assert.Contains("End date", vm.ErrorMessage);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        RequestRentalViewModel vm,
        Mock<IItemRepository> items,
        Mock<IRentalService> rentals,
        Mock<INavigationService> navigation) Build()
    {
        var items = new Mock<IItemRepository>();
        var rentals = new Mock<IRentalService>();
        // Pure helpers default-mocked: real CalculatePrice math via callback.
        rentals.Setup(s => s.CalculatePrice(It.IsAny<decimal>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
               .Returns<decimal, DateOnly, DateOnly>(
                   (rate, start, end) => rate * (end.DayNumber - start.DayNumber + 1));

        var nav = new Mock<INavigationService>();
        var vm = new RequestRentalViewModel(items.Object, rentals.Object, nav.Object);
        return (vm, items, rentals, nav);
    }

    private static Item SampleItem(int id, string title, decimal dailyRate) => new()
    {
        Id = id,
        Title = title,
        DailyRate = dailyRate,
        CategoryId = 1,
        OwnerId = 7,
        Latitude = 0,
        Longitude = 0,
        IsAvailable = true,
        OwnerName = "Ada",
    };
}
