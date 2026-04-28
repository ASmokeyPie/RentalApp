/// @file FindNearbyViewModel.cs
/// @brief "Find items near me" view model
/// @author RentalApp Development Team
/// @date 2026

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the location-based browse page.
/// @details Delegates the entire spatial-discovery workflow to
///          <see cref="ILocationService.FindNearbyItemsAsync"/> — the service
///          reads the device's GPS coordinates and queries the items API for
///          everything within the user-configured radius. The PostGIS work
///          happens server-side (<c>ST_DWithin</c>, <c>ST_MakePoint</c>
///          against the <c>GEOGRAPHY(POINT, 4326)</c> column on the items
///          table). The VM never sees lat/lon during the search itself; it
///          only stores the <c>SearchLatitude</c>/<c>SearchLongitude</c>
///          returned by the service so the page can render an info line.
///          MAUI-free for testability.
/// @extends BaseViewModel
public partial class FindNearbyViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    // The VM intentionally does NOT depend on IItemRepository — the spatial
    // workflow (GPS + radius query) is owned by ILocationService, keeping
    // coordinates out of the view layer per the project's spatial-logic rule.
    private readonly ILocationService _location = null!;
    private readonly ICategoryRepository _categories = null!;
    private readonly INavigationService _navigation = null!;

    /// @brief Items returned for the most recent search. Cleared on each
    ///        refresh, never paged (the API endpoint isn't paginated).
    public ObservableCollection<Item> Items { get; } = new();

    /// @brief Categories shown in the picker. <see cref="AllCategoriesOption"/>
    ///        is the synthetic top entry meaning "no filter".
    public ObservableCollection<Category> Categories { get; } = new();

    /// @brief Sentinel "all categories" entry inserted at the top of the
    ///        picker. Identified by <see cref="Category.Id"/> = 0 so the
    ///        category-slug filter is omitted when this is selected.
    public static Category AllCategoriesOption { get; } =
        new() { Id = 0, Name = "All categories", Slug = string.Empty };

    /// @brief Search radius in kilometres. Server caps at 50 (per spec).
    [ObservableProperty]
    private double radiusKm = 5;

    /// @brief Currently picked category, or <see cref="AllCategoriesOption"/>.
    [ObservableProperty]
    private Category? selectedCategory;

    /// @brief True while the RefreshView spinner should be active.
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief The lat/lon used for the most recent successful search. Used
    ///        for an info line ("Showing items near 55.95, -3.19") and so
    ///        we can re-search on category-change without another GPS read.
    [ObservableProperty]
    private double? searchLatitude;

    [ObservableProperty]
    private double? searchLongitude;

    /// @brief True when the empty-state placeholder should appear (no items
    ///        AND we've completed at least one search).
    [ObservableProperty]
    private bool isEmpty;

    /// @brief Default constructor for design-time support.
    public FindNearbyViewModel()
    {
        Title = "Find Nearby";
        Categories.Add(AllCategoriesOption);
        SelectedCategory = AllCategoriesOption;
    }

    public FindNearbyViewModel(
        ILocationService location,
        ICategoryRepository categories,
        INavigationService navigation)
        : this()
    {
        _location = location;
        _categories = categories;
        _navigation = navigation;
    }

    /// @brief Loads the categories list once for the picker. Page calls this
    ///        on first appearance; safe to call multiple times.
    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        try
        {
            ClearError();
            var loaded = await _categories.ListAsync();

            // Preserve the synthetic "All" entry at index 0; replace the rest.
            while (Categories.Count > 1) Categories.RemoveAt(1);
            foreach (var c in loaded) Categories.Add(c);
        }
        catch (Exception ex)
        {
            SetError($"Could not load categories: {ex.Message}");
        }
    }

    /// @brief Reads GPS, then queries the API for nearby items.
    /// @details Wired both as the page's pull-to-refresh handler and as the
    ///          "Search" / radius-slider change command. Replaces the items
    ///          collection on every successful run.
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            ClearError();

            var slug = SelectedCategory is { Id: > 0 } c ? c.Slug : null;
            var result = await _location.FindNearbyItemsAsync(RadiusKm, slug);

            if (result is null)
            {
                SetError("Couldn't read your location. Check that location is enabled and the app has permission.");
                IsEmpty = Items.Count == 0;
                return;
            }

            SearchLatitude = result.Latitude;
            SearchLongitude = result.Longitude;

            Items.Clear();
            foreach (var item in result.Items) Items.Add(item);
            IsEmpty = Items.Count == 0;
        }
        catch (Exception ex)
        {
            SetError($"Could not load nearby items: {ex.Message}");
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// @brief Navigates to the item-detail page for a tapped row.
    [RelayCommand]
    public async Task SelectItemAsync(Item? item)
    {
        if (item is null) return;
        await _navigation.NavigateToAsync(
            "ItemDetailsPage",
            new Dictionary<string, object> { ["itemId"] = item.Id });
    }
}
