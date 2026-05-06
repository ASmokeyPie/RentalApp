using Moq;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Services;
using RentalApp.ViewModels;

namespace RentalApp.Tests.ViewModels;

public class MyRentalsViewModelTests
{
    // ---- RefreshAsync -----------------------------------------------------

    [Fact]
    public async Task RefreshAsync_LoadsBothLists_AndPopulatesCollections()
    {
        // Arrange
        var (vm, rentals, _) = Build();
        rentals.Setup(s => s.GetIncomingAsync(null, default))
               .ReturnsAsync(new[] { Rental(1, "Drill", RentalStatus.Requested) });
        rentals.Setup(s => s.GetOutgoingAsync(null, default))
               .ReturnsAsync(new[]
               {
                   Rental(2, "Tent",   RentalStatus.Approved),
                   Rental(3, "Camera", RentalStatus.Completed),
               });

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Single(vm.Incoming);
        Assert.Equal(2, vm.Outgoing.Count);
        Assert.False(vm.IsRefreshing);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RefreshAsync_ReplacesExistingItems_OnSecondCall()
    {
         // Arrange
        var (vm, rentals, _) = Build();
        rentals.SetupSequence(s => s.GetIncomingAsync(null, default))
               .ReturnsAsync(new[] { Rental(1, "Drill", RentalStatus.Requested) })
               .ReturnsAsync(new[] { Rental(99, "Saw", RentalStatus.Approved) });
        rentals.Setup(s => s.GetOutgoingAsync(null, default))
               .ReturnsAsync(Array.Empty<Rental>());

         // Act
        await vm.RefreshAsync();
        await vm.RefreshAsync();

         // Assert
        Assert.Single(vm.Incoming);
        Assert.Equal(99, vm.Incoming[0].Id);
    }

    [Fact]
    public async Task RefreshAsync_ProceedsEvenWhenIsRefreshingAlreadyTrue()
    {
        // Same regression as the other refresh-driven VMs.
         // Arrange
        var (vm, rentals, _) = Build();
        rentals.Setup(s => s.GetIncomingAsync(null, default))
               .ReturnsAsync(Array.Empty<Rental>());
        rentals.Setup(s => s.GetOutgoingAsync(null, default))
               .ReturnsAsync(Array.Empty<Rental>());

        vm.IsRefreshing = true;

         // Act
        await vm.RefreshAsync();

         // Assert
        Assert.False(vm.IsRefreshing);
        rentals.Verify(s => s.GetIncomingAsync(null, default), Times.Once);
        rentals.Verify(s => s.GetOutgoingAsync(null, default), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_OnException_SetsError_AndClearsSpinner()
    {
        // Arrange
        var (vm, rentals, _) = Build();
        rentals.Setup(s => s.GetIncomingAsync(null, default))
               .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.True(vm.HasError);
        Assert.Contains("network down", vm.ErrorMessage);
        Assert.False(vm.IsRefreshing);
    }

    // ---- Tab selection ----------------------------------------------------

    [Fact]
    public void DefaultsToIncomingTab()
    {
        // Arrange
        var (vm, _, _) = Build();

        // Assert
        Assert.Equal("Incoming", vm.SelectedTab);
        Assert.True(vm.IsIncomingSelected);
        Assert.False(vm.IsOutgoingSelected);
    }

    [Fact]
    public void SelectOutgoing_FlipsBooleanFlags()
    {
        // Arrange
        var (vm, _, _) = Build();

        // Act
        vm.SelectOutgoing();

        // Assert
        Assert.Equal("Outgoing", vm.SelectedTab);
        Assert.False(vm.IsIncomingSelected);
        Assert.True(vm.IsOutgoingSelected);
    }

    [Fact]
    public async Task CurrentTabCount_FollowsActiveTab()
    {
        // Arrange
        var (vm, rentals, _) = Build();
        rentals.Setup(s => s.GetIncomingAsync(null, default))
               .ReturnsAsync(new[] { Rental(1, "Drill", RentalStatus.Requested) });
        rentals.Setup(s => s.GetOutgoingAsync(null, default))
               .ReturnsAsync(new[]
               {
                   Rental(2, "Tent",   RentalStatus.Approved),
                   Rental(3, "Camera", RentalStatus.Completed),
               });

        // Act
        await vm.RefreshAsync();

        // Assert
        Assert.Equal(1, vm.CurrentTabCount);   // Incoming has 1

        // Act
        vm.SelectOutgoing();

        // Assert
        Assert.Equal(2, vm.CurrentTabCount);   // Outgoing has 2
    }

    // ---- SelectRentalAsync ------------------------------------------------

    [Fact]
    public async Task SelectRentalAsync_NavigatesToRentalDetail()
    {
        // Arrange
        var (vm, _, nav) = Build();
        var rental = Rental(7, "Drill", RentalStatus.Approved);

        // Act
        await vm.SelectRentalAsync(rental);

        // Assert
        nav.Verify(n => n.NavigateToAsync(
                "RentalDetailsPage",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("rentalId") && (int)d["rentalId"] == 7)),
            Times.Once);
    }

    [Fact]
    public async Task SelectRentalAsync_TolerantOfNull()
    {
        // Arrange
        var (vm, _, nav) = Build();

        // Act
        await vm.SelectRentalAsync(null);

        // Assert
        nav.Verify(n => n.NavigateToAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
            Times.Never);
    }

    // ---- Helpers ----------------------------------------------------------

    private static (
        MyRentalsViewModel vm,
        Mock<IRentalService> rentals,
        Mock<INavigationService> navigation) Build()
    {
        var rentals = new Mock<IRentalService>();
        var nav = new Mock<INavigationService>();
        var vm = new MyRentalsViewModel(rentals.Object, nav.Object);
        return (vm, rentals, nav);
    }

    private static Rental Rental(int id, string itemTitle, RentalStatus status) => new()
    {
        Id = id,
        ItemId = 100 + id,
        BorrowerId = 5,
        StartDate = new DateOnly(2026, 5, 1),
        EndDate = new DateOnly(2026, 5, 3),
        Status = status,
        TotalPrice = 15m,
        ItemTitle = itemTitle,
        BorrowerName = "Bob",
        OwnerName = "Ada",
    };
}
