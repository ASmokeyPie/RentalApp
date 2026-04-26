/// @file ItemsListViewModel.cs
/// @brief Browse-items list view model with pagination + pull-to-refresh
/// @author RentalApp Development Team
/// @date 2026

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the paginated browse-items list.
/// @details Loads items via <see cref="IItemRepository.SearchAsync"/> one page
///          at a time. Supports pull-to-refresh (resets to page 1 and replaces
///          the collection) and "load more" (appends the next page). Tapping
///          an item navigates to <c>ItemDetailsPage</c> with the item id.
///          MAUI-free so it can be unit-tested against the .NET 10 test
///          project; visual concerns (RefreshView, CollectionView, alerts)
///          live in the XAML page.
/// @extends BaseViewModel
public partial class ItemsListViewModel : BaseViewModel
{
    private readonly IItemRepository _items;
    private readonly INavigationService _navigation;

    /// @brief Items currently loaded into the list. Pages are appended in
    ///        load order, so this grows as the user scrolls.
    public ObservableCollection<Item> Items { get; } = new();

    /// @brief 1-based page number of the most recently loaded page.
    [ObservableProperty]
    private int currentPage = 0;

    /// @brief Total pages reported by the server (0 until the first response).
    [ObservableProperty]
    private int totalPages = 0;

    /// @brief Total item count reported by the server (used for "X items" labels).
    [ObservableProperty]
    private int totalCount = 0;

    /// @brief Items per page sent on every request. Repository default is 20.
    [ObservableProperty]
    private int pageSize = 20;

    /// @brief True while a load-more operation is in flight (separate from
    ///        <see cref="BaseViewModel.IsBusy"/> so pull-to-refresh and
    ///        infinite scroll can render distinct UI affordances).
    [ObservableProperty]
    private bool isLoadingMore;

    /// @brief True if the loaded set is empty AND we're not currently busy
    ///        loading (so the page can show an "empty state" placeholder).
    [ObservableProperty]
    private bool isEmpty;

    /// @brief True when the next-page button/scroll-trigger should be enabled.
    public bool HasMorePages => CurrentPage > 0 && CurrentPage < TotalPages;

    /// @brief Default constructor for design-time support.
    public ItemsListViewModel()
    {
        Title = "Browse Items";
    }

    /// @brief Initializes a new instance of the <see cref="ItemsListViewModel"/> class.
    /// @param items Item repository (API or DB-backed; the VM doesn't care).
    /// @param navigation Navigation service used to push the detail page.
    public ItemsListViewModel(IItemRepository items, INavigationService navigation)
    {
        _items = items;
        _navigation = navigation;
        Title = "Browse Items";
    }

    /// @brief Loads (or reloads) the first page, replacing any current items.
    /// @details Invoked on page appearing and as the pull-to-refresh handler.
    /// @return A task that completes when the first page has been loaded.
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var page = await _items.SearchAsync(new ItemQuery { Page = 1, PageSize = PageSize });

            Items.Clear();
            foreach (var i in page.Items) Items.Add(i);

            CurrentPage = page.Page;
            TotalPages  = page.TotalPages;
            TotalCount  = page.TotalCount;
            IsEmpty     = Items.Count == 0;
            OnPropertyChanged(nameof(HasMorePages));
        }
        catch (Exception ex)
        {
            SetError($"Could not load items: {ex.Message}");
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Appends the next page of results to the existing collection.
    /// @details No-op when there are no more pages or another load is in
    ///          flight. Suitable to wire to a CollectionView's
    ///          <c>RemainingItemsThreshold</c> trigger or a "Load more" button.
    /// @return A task that completes once the next page is appended.
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsBusy || IsLoadingMore) return;
        if (!HasMorePages) return;

        try
        {
            IsLoadingMore = true;
            ClearError();

            var nextPage = CurrentPage + 1;
            var page = await _items.SearchAsync(new ItemQuery { Page = nextPage, PageSize = PageSize });

            foreach (var i in page.Items) Items.Add(i);

            CurrentPage = page.Page;
            TotalPages  = page.TotalPages;
            TotalCount  = page.TotalCount;
            OnPropertyChanged(nameof(HasMorePages));
        }
        catch (Exception ex)
        {
            SetError($"Could not load more items: {ex.Message}");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// @brief Navigates to the item details page for the selected item.
    /// @param item The item the user tapped. Tolerates null (e.g. if the
    ///        binding fires before a real selection is made).
    /// @return A task that completes once navigation has been pushed.
    [RelayCommand]
    public async Task SelectItemAsync(Item? item)
    {
        if (item is null) return;

        await _navigation.NavigateToAsync(
            "ItemDetailsPage",
            new Dictionary<string, object> { ["itemId"] = item.Id });
    }

    /// @brief Navigates to the create-item form.
    /// @details Bound to the "+" toolbar item on the list page so users can
    ///          jump straight from browsing to listing without first popping
    ///          back to the dashboard.
    [RelayCommand]
    public async Task NavigateToCreateItemAsync()
    {
        await _navigation.NavigateToAsync("CreateItemPage");
    }

    // ---- Property change hooks --------------------------------------------

    partial void OnCurrentPageChanged(int value) =>
        OnPropertyChanged(nameof(HasMorePages));

    partial void OnTotalPagesChanged(int value) =>
        OnPropertyChanged(nameof(HasMorePages));
}
