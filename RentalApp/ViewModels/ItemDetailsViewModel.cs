/// @file ItemDetailsViewModel.cs
/// @brief Item-detail view model populated from a Shell route parameter
/// @author RentalApp Development Team
/// @date 2026

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;

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
    private readonly IItemRepository _items;

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

    /// @brief Default constructor for design-time support.
    public ItemDetailsViewModel()
    {
        Title = "Item";
    }

    /// @brief Initializes a new instance of the <see cref="ItemDetailsViewModel"/> class.
    /// @param items Item repository used to fetch the item by id.
    public ItemDetailsViewModel(IItemRepository items)
    {
        _items = items;
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
                Title = "Item";
                return;
            }

            Item = loaded;
            IsLoaded = true;
            Title = loaded.Title;
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
