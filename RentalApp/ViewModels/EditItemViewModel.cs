/// @file EditItemViewModel.cs
/// @brief View model for the "edit item" form (owner-only)
/// @author RentalApp Development Team
/// @date 2026

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model backing the edit-item form.
/// @details Loads the item by id (set via the Shell route parameter "itemId"
///          on the page), pre-populates form fields, and submits via
///          <see cref="IItemRepository.UpdateAsync"/>. The PUT spec only
///          accepts <c>title</c>, <c>description</c>, <c>dailyRate</c>, and
///          <c>isAvailable</c> — other fields surface read-only.
///          Performs a defensive owner-only check after load: if the
///          authenticated user isn't the item's owner, the form is locked
///          and an error is displayed (in addition to the entry-point being
///          hidden on the detail page).
///          MAUI-free for testability — Shell route binding lives on the page.
/// @extends BaseViewModel
public partial class EditItemViewModel : BaseViewModel
{
    private readonly IItemRepository _items;
    private readonly IAuthenticationService _auth;
    private readonly INavigationService _navigation;

    /// @brief Set by the page from the Shell route parameter.
    [ObservableProperty]
    private int itemId;

    /// @brief The currently loaded item (used for read-only display).
    [ObservableProperty]
    private Item? originalItem;

    /// @brief True once the item has been successfully fetched and the
    ///        current user has been verified as its owner.
    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    private string itemTitle = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string dailyRateText = string.Empty;

    [ObservableProperty]
    private bool isAvailable = true;

    /// @brief Default constructor for design-time support.
    public EditItemViewModel()
    {
        Title = "Edit Item";
    }

    public EditItemViewModel(
        IItemRepository items,
        IAuthenticationService auth,
        INavigationService navigation)
    {
        _items = items;
        _auth = auth;
        _navigation = navigation;
        Title = "Edit Item";
    }

    /// @brief Loads the item by id and verifies ownership.
    /// @details Triggered automatically by <see cref="OnItemIdChanged"/> when
    ///          the page hands over the route parameter. Also wired as the
    ///          page's pull-to-refresh command.
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
                OriginalItem = null;
                CanEdit = false;
                SetError($"Item {ItemId} could not be found.");
                return;
            }

            var currentUserId = _auth.CurrentUser?.Id ?? 0;
            if (currentUserId == 0 || loaded.OwnerId != currentUserId)
            {
                OriginalItem = loaded;
                CanEdit = false;
                SetError("You can only edit items you own.");
                return;
            }

            OriginalItem = loaded;
            CanEdit = true;

            // Pre-populate the form fields.
            ItemTitle      = loaded.Title;
            Description    = loaded.Description ?? string.Empty;
            DailyRateText  = loaded.DailyRate.ToString("0.##", CultureInfo.InvariantCulture);
            IsAvailable    = loaded.IsAvailable;
            Title          = $"Edit: {loaded.Title}";
        }
        catch (Exception ex)
        {
            OriginalItem = null;
            CanEdit = false;
            SetError($"Could not load item: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Validates the form and PUTs the updated item.
    /// @details Submit is blocked unless <see cref="CanEdit"/> is true (i.e.
    ///          load succeeded AND the current user is the owner). On success
    ///          the page navigates back.
    [RelayCommand]
    public async Task SubmitAsync()
    {
        if (IsBusy) return;
        if (!CanEdit || OriginalItem is null)
        {
            SetError("You can only edit items you own.");
            return;
        }

        if (!TryBuildItem(out var updated, out var error))
        {
            SetError(error);
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            await _items.UpdateAsync(updated);

            await _navigation.NavigateBackAsync();
        }
        catch (Exception ex)
        {
            SetError($"Could not save changes: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Builds the Item to submit. Only title/description/dailyRate/
    ///        isAvailable are editable; everything else is preserved from
    ///        the loaded original.
    internal bool TryBuildItem(out Item item, out string error)
    {
        item = null!;
        if (OriginalItem is null)
        {
            error = "Item not loaded.";
            return false;
        }

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

        // Clone the original and mutate the editable fields. Other fields
        // (CategoryId, OwnerId, lat/lon, CreatedAt) round-trip unchanged.
        item = new Item
        {
            Id = OriginalItem.Id,
            Title = trimmedTitle,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            DailyRate = rate,
            CategoryId = OriginalItem.CategoryId,
            OwnerId = OriginalItem.OwnerId,
            Latitude = OriginalItem.Latitude,
            Longitude = OriginalItem.Longitude,
            IsAvailable = IsAvailable,
            CreatedAt = OriginalItem.CreatedAt,
            UpdatedAt = OriginalItem.UpdatedAt,
        };
        error = string.Empty;
        return true;
    }

    // ---- Property change hooks --------------------------------------------

    partial void OnItemIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadAsync();
        }
    }
}
