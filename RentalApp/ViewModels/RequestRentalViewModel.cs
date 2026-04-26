/// @file RequestRentalViewModel.cs
/// @brief View model for the rental-request form
/// @author RentalApp Development Team
/// @date 2026

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model backing the rent-this-item request form.
/// @details Reached via Shell route with an <c>itemId</c> query parameter
///          (forwarded by <c>RequestRentalPage.ApplyQueryAttributes</c>). Loads
///          the item for display, lets the user pick start/end dates, shows a
///          live total price using <see cref="IRentalService.CalculatePrice"/>,
///          and submits via <see cref="IRentalService.RequestRentalAsync"/>.
///          The service handles client-side date validation; server 409 (e.g.
///          double-booking) is surfaced via the error banner.
///          MAUI-free for testability.
/// @extends BaseViewModel
public partial class RequestRentalViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IItemRepository _items = null!;
    private readonly IRentalService _rentals = null!;
    private readonly INavigationService _navigation = null!;

    /// @brief Set by the page from the Shell route parameter.
    [ObservableProperty]
    private int itemId;

    /// @brief The item being rented (used for read-only display + price calc).
    [ObservableProperty]
    private Item? item;

    /// @brief True once the item has been loaded successfully.
    [ObservableProperty]
    private bool isLoaded;

    /// @brief Rental start date. Defaults to today; a partial method recomputes
    ///        the total price whenever this changes.
    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    /// @brief Rental end date. Defaults to tomorrow.
    [ObservableProperty]
    private DateTime endDate = DateTime.Today.AddDays(1);

    /// @brief Earliest selectable start date (today). Bound to DatePicker.MinimumDate.
    public DateTime MinimumStartDate { get; } = DateTime.Today;

    /// @brief Earliest selectable end date (always ≥ current StartDate).
    public DateTime MinimumEndDate => StartDate;

    /// @brief Inclusive number of rental days. Drives the price-summary line.
    public int TotalDays
    {
        get
        {
            if (EndDate < StartDate) return 0;
            return (EndDate.Date - StartDate.Date).Days + 1;
        }
    }

    /// @brief Total price for the selected range at the loaded item's rate.
    public decimal TotalPrice
    {
        get
        {
            if (Item is null || EndDate < StartDate || _rentals is null) return 0m;
            return _rentals.CalculatePrice(
                Item.DailyRate,
                DateOnly.FromDateTime(StartDate),
                DateOnly.FromDateTime(EndDate));
        }
    }

    /// @brief Default constructor for design-time support.
    public RequestRentalViewModel()
    {
        Title = "Rent item";
    }

    public RequestRentalViewModel(
        IItemRepository items,
        IRentalService rentals,
        INavigationService navigation)
    {
        _items = items;
        _rentals = rentals;
        _navigation = navigation;
        Title = "Rent item";
    }

    /// @brief Loads the item by id for display.
    /// @details Triggered automatically by <see cref="OnItemIdChanged"/> when
    ///          the page hands over the route parameter. Also wired to
    ///          pull-to-refresh.
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (ItemId <= 0) return;
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var loaded = await _items.GetByIdAsync(ItemId);
            if (loaded is null)
            {
                Item = null;
                IsLoaded = false;
                SetError($"Item {ItemId} could not be found.");
                return;
            }

            Item = loaded;
            IsLoaded = true;
            Title = $"Rent: {loaded.Title}";
        }
        catch (Exception ex)
        {
            Item = null;
            IsLoaded = false;
            SetError($"Could not load item: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Submits the rental request via the service.
    /// @details Date validation lives in the service; client-side errors
    ///          surface via the error banner. A successful submit pops the
    ///          page so the user lands back on the item detail.
    [RelayCommand]
    public async Task SubmitAsync()
    {
        if (IsBusy) return;
        if (!IsLoaded || Item is null)
        {
            SetError("Item not loaded yet.");
            return;
        }
        if (EndDate < StartDate)
        {
            SetError("End date must be on or after start date.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            await _rentals.RequestRentalAsync(
                Item.Id,
                DateOnly.FromDateTime(StartDate),
                DateOnly.FromDateTime(EndDate));

            await _navigation.NavigateBackAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Service-level validation (past start, end<start). Already friendly.
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            // Wire / server errors. 409 from the API surfaces here too.
            SetError($"Could not submit rental: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Property-change hooks --------------------------------------------

    partial void OnItemIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadAsync();
        }
    }

    partial void OnStartDateChanged(DateTime value)
    {
        // If the user moves Start past End, drag End along so the range stays valid.
        if (EndDate < value)
        {
            EndDate = value;
        }
        OnPropertyChanged(nameof(MinimumEndDate));
        OnPropertyChanged(nameof(TotalDays));
        OnPropertyChanged(nameof(TotalPrice));
    }

    partial void OnEndDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(TotalDays));
        OnPropertyChanged(nameof(TotalPrice));
    }

    partial void OnItemChanged(Item? value)
    {
        OnPropertyChanged(nameof(TotalPrice));
    }
}
