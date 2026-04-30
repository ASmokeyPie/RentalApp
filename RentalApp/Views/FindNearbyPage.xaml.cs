using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using RentalApp.ViewModels;

namespace RentalApp.Views;

public partial class FindNearbyPage : ContentPage
{
    private readonly FindNearbyViewModel _viewModel;

    public FindNearbyPage(FindNearbyViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Repopulate pins whenever the items collection changes.
        _viewModel.Items.CollectionChanged += OnItemsChanged;

        // Also re-centre the map when the search coordinates arrive.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// @brief Loads categories on first appearance.
    /// @details The first GPS fetch happens when the user taps "Find nearby
    ///          items" — we don't auto-prompt for location permission on
    ///          page-open because that's a surprising experience.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Categories.Count <= 1)  // only the synthetic "All" entry
        {
            await _viewModel.LoadCategoriesAsync();
        }
    }

    // ---- Map pin management -------------------------------------------------

    /// @brief Rebuilds all map pins whenever the Items list is replaced.
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshPins();

    /// @brief Centres the map when SearchLatitude/SearchLongitude arrive after
    ///        a successful GPS + API round-trip.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FindNearbyViewModel.SearchLatitude)
                           or nameof(FindNearbyViewModel.SearchLongitude))
        {
            CentreMap();
        }
    }

    /// @brief Clears existing pins and adds one per item plus a "you are here"
    ///        origin pin at the search coordinates.
    private void RefreshPins()
    {
        NearbyMap.Pins.Clear();

        // Item pins.
        foreach (var item in _viewModel.Items)
        {
            var pin = new Pin
            {
                Label   = item.Title,
                Address = $"£{item.DailyRate:F2}/day · {item.CategoryName}",
                Type    = PinType.Place,
                Location = new Location(item.Latitude, item.Longitude),
            };

            // Navigate to the item-detail page when the info window is tapped.
            pin.MarkerClicked += (s, args) =>
            {
                args.HideInfoWindow = false;   // keep window open for the tap
            };
            pin.InfoWindowClicked += (s, args) =>
            {
                _viewModel.SelectItemCommand.Execute(item);
            };

            NearbyMap.Pins.Add(pin);
        }

        // Search-origin pin (only when coordinates are available).
        if (_viewModel.SearchLatitude is { } lat && _viewModel.SearchLongitude is { } lon)
        {
            NearbyMap.Pins.Add(new Pin
            {
                Label    = "Search origin",
                Type     = PinType.SearchResult,
                Location = new Location(lat, lon),
            });
        }
    }

    /// @brief Moves the map viewport to the search origin at a zoom level
    ///        that fits the chosen radius.
    private void CentreMap()
    {
        if (_viewModel.SearchLatitude is not { } lat
         || _viewModel.SearchLongitude is not { } lon) return;

        NearbyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                new Location(lat, lon),
                Distance.FromKilometers(_viewModel.RadiusKm)));
    }
}
