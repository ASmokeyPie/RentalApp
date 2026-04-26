/// @file CreateItemViewModel.cs
/// @brief View model for the "create item" form
/// @author RentalApp Development Team
/// @date 2026

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model backing the new-item creation form.
/// @details Loads categories for the picker on construction, validates form
///          state against the API constraints (title 5-100 chars, dailyRate
///          0&lt;x≤1000, lat/lon ranges), and submits via
///          <see cref="IItemRepository.CreateAsync"/>. On success the page
///          navigates back. Lat/lon are entered manually for now; Phase 6
///          will retrofit GPS auto-fill via LocationService.
///          MAUI-free for testability.
/// @extends BaseViewModel
public partial class CreateItemViewModel : BaseViewModel
{
    private readonly IItemRepository _items;
    private readonly ICategoryRepository _categories;
    private readonly INavigationService _navigation;

    /// @brief Categories shown in the picker. Populated on first load.
    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty]
    private Category? selectedCategory;

    [ObservableProperty]
    private string itemTitle = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    /// @brief Daily rate as raw user input. Parsed at submit-time to decimal
    ///        so empty input doesn't silently map to 0.
    [ObservableProperty]
    private string dailyRateText = string.Empty;

    [ObservableProperty]
    private string latitudeText = string.Empty;

    [ObservableProperty]
    private string longitudeText = string.Empty;

    /// @brief True while categories are loading (separate spinner from the
    ///        global IsBusy which doubles for submit-in-flight).
    [ObservableProperty]
    private bool isLoadingCategories;

    /// @brief Default constructor for design-time support.
    public CreateItemViewModel()
    {
        Title = "New Item";
    }

    public CreateItemViewModel(
        IItemRepository items,
        ICategoryRepository categories,
        INavigationService navigation)
    {
        _items = items;
        _categories = categories;
        _navigation = navigation;
        Title = "New Item";
    }

    /// @brief Loads (or reloads) the categories list for the picker.
    /// @details Called by the page on first appearance. Safe to call multiple
    ///          times — replaces the collection rather than appending.
    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        if (IsLoadingCategories) return;

        try
        {
            IsLoadingCategories = true;
            ClearError();

            var loaded = await _categories.ListAsync();
            Categories.Clear();
            foreach (var c in loaded) Categories.Add(c);
        }
        catch (Exception ex)
        {
            SetError($"Could not load categories: {ex.Message}");
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    /// @brief Validates the form and submits a new item to the repository.
    /// @details On success, navigates back; on failure, leaves form intact
    ///          with an error message. IsBusy guards against double-submit.
    [RelayCommand]
    public async Task SubmitAsync()
    {
        if (IsBusy) return;
        if (!TryBuildItem(out var item, out var error))
        {
            SetError(error);
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            await _items.CreateAsync(item);

            await _navigation.NavigateBackAsync();
        }
        catch (Exception ex)
        {
            SetError($"Could not create item: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Validates user input and constructs an <see cref="Item"/> ready
    ///        to send to the API.
    /// @param item The constructed item, or null if validation failed.
    /// @param error A user-facing error message, or empty on success.
    /// @return True if the form is valid, false otherwise.
    internal bool TryBuildItem(out Item item, out string error)
    {
        item = null!;

        var trimmedTitle = ItemTitle?.Trim() ?? string.Empty;
        if (trimmedTitle.Length < 5 || trimmedTitle.Length > 100)
        {
            error = "Title must be between 5 and 100 characters.";
            return false;
        }

        if (Description is { Length: > 1000 })
        {
            error = "Description must be 1000 characters or fewer.";
            return false;
        }

        if (!decimal.TryParse(DailyRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
            || rate <= 0m || rate > 1000m)
        {
            error = "Daily rate must be a number greater than 0 and at most 1000.";
            return false;
        }

        if (SelectedCategory is null)
        {
            error = "Pick a category.";
            return false;
        }

        if (!double.TryParse(LatitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || lat < -90 || lat > 90)
        {
            error = "Latitude must be a number between -90 and 90.";
            return false;
        }

        if (!double.TryParse(LongitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            || lon < -180 || lon > 180)
        {
            error = "Longitude must be a number between -180 and 180.";
            return false;
        }

        item = new Item
        {
            Title = trimmedTitle,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            DailyRate = rate,
            CategoryId = SelectedCategory.Id,
            Latitude = lat,
            Longitude = lon,
        };
        error = string.Empty;
        return true;
    }
}
