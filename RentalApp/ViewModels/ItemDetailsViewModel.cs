/// @file ItemDetailsViewModel.cs
/// @brief Item-detail view model populated from a Shell route parameter
/// @author RentalApp Development Team
/// @date 2026

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the item-details page.
/// @details The page is reached via Shell navigation with an <c>itemId</c>
///          query parameter (e.g.
///          <c>Shell.Current.GoToAsync("ItemDetailsPage", new() { ["itemId"] = 42 })</c>).
///          The <c>ItemDetailsPage</c> code-behind implements
///          <c>IQueryAttributable</c> and assigns the parsed id to
///          <see cref="ItemId"/>; the partial method
///          <see cref="OnItemIdChanged"/> kicks off a load when the id arrives.
///          MAUI-free for testability — the route-parameter wiring lives on
///          the page so this class compiles in the .NET 10 test project.
/// @extends BaseViewModel
public partial class ItemDetailsViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IItemRepository _items = null!;
    private readonly IAuthenticationService? _auth;
    private readonly INavigationService? _navigation;

    /// @brief The id of the item to load. Set by Shell navigation.
    /// @details Setting this property triggers an asynchronous load of the
    ///          item via <see cref="LoadAsync"/>; the page's binding to
    ///          <see cref="Item"/> updates when the load completes.
    [ObservableProperty]
    private int itemId;

    /// @brief The loaded item, or null while loading / on error.
    [ObservableProperty]
    private Item? item;

    /// @brief True once the item has loaded successfully (page can show the
    ///        details Grid; a complementary <see cref="BaseViewModel.IsBusy"/>
    ///        renders the spinner during the initial load).
    [ObservableProperty]
    private bool isLoaded;

    /// @brief True when the authenticated user is the owner of the loaded
    ///        item. Drives the visibility of the Edit entry point on the page.
    [ObservableProperty]
    private bool isOwner;

    /// @brief True when the authenticated user can rent this item.
    /// @details Computed from <see cref="IsLoaded"/>, <see cref="IsOwner"/>,
    ///          and the item's availability + auth state. Drives the
    ///          visibility of the "Rent this" button. Updated whenever any of
    ///          its inputs change (see the partial methods below).
    [ObservableProperty]
    private bool canRent;

    /// @brief True while the RefreshView spinner should be active.
    /// @details Bound two-way to <c>RefreshView.IsRefreshing</c>. The
    ///          RefreshView toggles this <i>before</i> firing the command,
    ///          so <see cref="LoadAsync"/> must NOT early-return on it.
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief Default constructor for design-time support.
    public ItemDetailsViewModel()
    {
        Title = "Item";
    }

    /// @brief Initializes a new instance of the <see cref="ItemDetailsViewModel"/> class.
    /// @param items Item repository used to fetch the item by id.
    /// @param auth Authentication service, used to check ownership.
    /// @param navigation Navigation service, used by the EditCommand.
    public ItemDetailsViewModel(
        IItemRepository items,
        IAuthenticationService auth,
        INavigationService navigation)
    {
        _items = items;
        _auth = auth;
        _navigation = navigation;
        Title = "Item";
    }

    /// @brief Reloads the current item by id.
    /// @details Wired as the page's pull-to-refresh command; also called
    ///          internally when <see cref="ItemId"/> changes.
    /// @return A task that completes once the load attempt finishes.
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (ItemId <= 0)
        {
            // Shell may set ItemId=0 transiently before the real value arrives.
            return;
        }
        // No early-return on IsRefreshing: the RefreshView pre-toggles it
        // before firing this command, so bailing here would leave the spinner
        // stuck. Concurrent loads are harmless — at worst one redundant fetch.

        try
        {
            IsRefreshing = true;
            ClearError();

            var loaded = await _items.GetByIdAsync(ItemId);
            if (loaded is null)
            {
                Item = null;
                IsLoaded = false;
                IsOwner = false;
                CanRent = false;
                SetError($"Item {ItemId} could not be found.");
                Title = "Item";
                return;
            }

            Item = loaded;
            IsLoaded = true;
            Title = loaded.Title;

            var currentUserId = _auth?.CurrentUser?.Id ?? 0;
            IsOwner = currentUserId != 0 && loaded.OwnerId == currentUserId;
            CanRent = IsLoaded
                      && !IsOwner
                      && loaded.IsAvailable
                      && _auth?.IsAuthenticated == true;
        }
        catch (Exception ex)
        {
            Item = null;
            IsLoaded = false;
            IsOwner = false;
            CanRent = false;
            SetError($"Could not load item: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// @brief Navigates to the edit-item page for the loaded item.
    /// @details Owner-only — guarded by <see cref="IsOwner"/> at the binding
    ///          level (button is hidden when false) and again here as a
    ///          defensive check in case the command is invoked directly.
    [RelayCommand]
    public async Task EditItemAsync()
    {
        if (!IsOwner || Item is null) return;
        if (_navigation is null) return;

        await _navigation.NavigateToAsync(
            "EditItemPage",
            new Dictionary<string, object> { ["itemId"] = Item.Id });
    }

    /// @brief Navigates to the request-rental page for the loaded item.
    /// @details Guarded by <see cref="CanRent"/> at the binding level (button
    ///          is hidden when false) and again here defensively. Authenticated
    ///          non-owners only — owners can't rent their own items.
    [RelayCommand]
    public async Task RentItemAsync()
    {
        if (!CanRent || Item is null) return;
        if (_navigation is null) return;

        await _navigation.NavigateToAsync(
            "RequestRentalPage",
            new Dictionary<string, object> { ["itemId"] = Item.Id });
    }

    // ---- Property change hooks --------------------------------------------

    /// @brief Triggered by the source generator when ItemId changes.
    /// @details Fires a load whenever Shell sets the route parameter (or the
    ///          property is reassigned manually). Fire-and-forget by design —
    ///          callers that want to await the load can call
    ///          <see cref="LoadAsync"/> directly.
    partial void OnItemIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadAsync();
        }
    }
}
