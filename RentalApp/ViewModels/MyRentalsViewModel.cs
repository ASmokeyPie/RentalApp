/// @file MyRentalsViewModel.cs
/// @brief Tabbed Incoming/Outgoing rentals list view model
/// @author RentalApp Development Team
/// @date 2026

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model backing the "My Rentals" page.
/// @details Two tabs share the page:
/// <list type="bullet">
///   <item><description><b>Incoming</b> — rentals on items the current user owns
///     (requests they've received).</description></item>
///   <item><description><b>Outgoing</b> — rentals the current user has placed on
///     other owners' items (requests they've sent).</description></item>
/// </list>
/// The two collections are fetched in parallel via
/// <see cref="IRentalService.GetIncomingAsync"/> /
/// <see cref="IRentalService.GetOutgoingAsync"/>; the page swaps which one is
/// visible based on the selected tab. Tapping a row currently navigates to
/// the rental's item detail page (Phase 5d will replace that with a dedicated
/// rental-detail page once owner action commands land).
/// MAUI-free for testability.
/// @extends BaseViewModel
public partial class MyRentalsViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IRentalService _rentals = null!;
    private readonly INavigationService _navigation = null!;

    /// @brief Rentals on items the current user owns.
    public ObservableCollection<Rental> Incoming { get; } = new();

    /// @brief Rentals the current user has placed on others' items.
    public ObservableCollection<Rental> Outgoing { get; } = new();

    /// @brief Selected tab name; either "Incoming" or "Outgoing". A string
    ///        rather than an enum so XAML bindings stay simple.
    [ObservableProperty]
    private string selectedTab = "Incoming";

    /// @brief True while the RefreshView spinner should be active.
    /// @details Bound two-way to <c>RefreshView.IsRefreshing</c> — see the
    ///          equivalent property on the other refresh-driven VMs for why
    ///          this is separate from <c>IsBusy</c>.
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief True iff the Incoming tab is currently selected.
    public bool IsIncomingSelected => SelectedTab == "Incoming";

    /// @brief True iff the Outgoing tab is currently selected.
    public bool IsOutgoingSelected => SelectedTab == "Outgoing";

    /// @brief Item count for the currently selected tab. Drives the empty-state
    ///        placeholder.
    public int CurrentTabCount =>
        IsIncomingSelected ? Incoming.Count : Outgoing.Count;

    /// @brief Default constructor for design-time support.
    public MyRentalsViewModel()
    {
        Title = "My Rentals";
        Incoming.CollectionChanged += (_, __) => OnPropertyChanged(nameof(CurrentTabCount));
        Outgoing.CollectionChanged += (_, __) => OnPropertyChanged(nameof(CurrentTabCount));
    }

    public MyRentalsViewModel(IRentalService rentals, INavigationService navigation)
        : this()
    {
        _rentals = rentals;
        _navigation = navigation;
    }

    /// @brief Loads (or reloads) both incoming and outgoing rentals in parallel.
    /// @details Does NOT early-return on <see cref="IsRefreshing"/> — that's
    ///          set by the RefreshView before this fires. Each list is replaced
    ///          atomically (not appended) so a refresh always reflects server
    ///          state at the moment the requests landed.
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            ClearError();

            var incomingTask = _rentals.GetIncomingAsync();
            var outgoingTask = _rentals.GetOutgoingAsync();
            await Task.WhenAll(incomingTask, outgoingTask);

            Incoming.Clear();
            foreach (var r in incomingTask.Result) Incoming.Add(r);

            Outgoing.Clear();
            foreach (var r in outgoingTask.Result) Outgoing.Add(r);

            OnPropertyChanged(nameof(CurrentTabCount));
        }
        catch (Exception ex)
        {
            SetError($"Could not load rentals: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// @brief Switches to the Incoming tab.
    [RelayCommand]
    public void SelectIncoming() => SelectedTab = "Incoming";

    /// @brief Switches to the Outgoing tab.
    [RelayCommand]
    public void SelectOutgoing() => SelectedTab = "Outgoing";

    /// @brief Navigates to the rental's item detail page.
    /// @details Phase 5d will swap this to a dedicated rental-detail page.
    [RelayCommand]
    public async Task SelectRentalAsync(Rental? rental)
    {
        if (rental is null) return;

        await _navigation.NavigateToAsync(
            "ItemDetailsPage",
            new Dictionary<string, object> { ["itemId"] = rental.ItemId });
    }

    // ---- Property change hooks --------------------------------------------

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsIncomingSelected));
        OnPropertyChanged(nameof(IsOutgoingSelected));
        OnPropertyChanged(nameof(CurrentTabCount));
    }
}
