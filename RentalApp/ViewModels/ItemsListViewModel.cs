/// @file ItemsListViewModel.cs
/// @brief Browse-items list view model with pull-to-refresh + Load-more button
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
///          at a time. Pull-to-refresh resets to page 1 and replaces the
///          collection; an explicit <see cref="LoadMoreCommand"/> button
///          appends the next page (no infinite-scroll trigger — that proved
///          flaky in practice and conflated with the refresh state). Tapping
///          an item navigates to <c>ItemDetailsPage</c>.
///          MAUI-free so it can be unit-tested against the .NET 10 test
///          project; visual concerns (RefreshView, CollectionView) live in
///          the XAML page.
/// @extends BaseViewModel
public partial class ItemsListViewModel : BaseViewModel
{
    private readonly IItemRepository _items;
    private readonly INavigationService _navigation;

    /// @brief Items currently loaded into the list. Pages are appended in
    ///        load order. Page-level changes (CollectionChanged) refresh the
    ///        derived properties below.
    public ObservableCollection<Item> Items { get; }

    /// @brief 1-based page number of the most recently loaded page.
    [ObservableProperty]
    private int currentPage = 0;

    /// @brief Total pages reported by the server (0 until the first response).
    [ObservableProperty]
    private int totalPages = 0;

    /// @brief Total item count reported by the server.
    [ObservableProperty]
    private int totalCount = 0;

    /// @brief Items per page sent on every request. Default 50 — closer to
    ///        the API's 100 cap than the old 20 so the user makes fewer
    ///        Load-more taps to see everything.
    [ObservableProperty]
    private int pageSize = 50;

    /// @brief True while the RefreshView spinner should be active.
    /// @details Bound two-way to <c>RefreshView.IsRefreshing</c>. The
    ///          RefreshView toggles this <i>before</i> firing
    ///          <see cref="RefreshCommand"/>, so <see cref="RefreshAsync"/>
    ///          must NOT early-return on it (an earlier IsBusy-based guard
    ///          caused the spinner to spin forever).
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief True while a Load-more operation is in flight.
    [ObservableProperty]
    private bool isLoadingMore;

    /// @brief True if the loaded set is empty AND we're not currently
    ///        refreshing (so the page can show an "empty state" placeholder).
    [ObservableProperty]
    private bool isEmpty;

    /// @brief True when there is at least one further page to fetch.
    public bool HasMorePages => CurrentPage > 0 && CurrentPage < TotalPages;

    /// @brief Number of items left on the server beyond what's loaded.
    /// @details Drives the "Load more (N remaining)" button label.
    public int RemainingCount => Math.Max(0, TotalCount - Items.Count);

    /// @brief Default constructor for design-time support.
    public ItemsListViewModel()
    {
        Items = new ObservableCollection<Item>();
        Items.CollectionChanged += (_, __) =>
        {
            // Items.Count changed → both derived properties may need to
            // re-evaluate in bindings.
            OnPropertyChanged(nameof(RemainingCount));
            OnPropertyChanged(nameof(HasMorePages));
        };
        Title = "Browse Items";
    }

    /// @brief Initializes a new instance of the <see cref="ItemsListViewModel"/> class.
    /// @param items Item repository (API or DB-backed; the VM doesn't care).
    /// @param navigation Navigation service used to push the detail page.
    public ItemsListViewModel(IItemRepository items, INavigationService navigation)
        : this()
    {
        _items = items;
        _navigation = navigation;
    }

    /// @brief Loads (or reloads) the first page, replacing any current items.
    /// @details Invoked on page appearing AND as the pull-to-refresh handler.
    ///          Does NOT early-return on <see cref="IsRefreshing"/> because
    ///          the RefreshView sets that to true before firing the command —
    ///          early-returning here would leave the spinner stuck. Guards
    ///          only against a concurrent Load-more.
    /// @return A task that completes when the first page has been loaded.
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoadingMore) return;

        try
        {
            IsRefreshing = true;
            ClearError();

            var page = await _items.SearchAsync(new ItemQuery { Page = 1, PageSize = PageSize });

            Items.Clear();
            foreach (var i in page.Items) Items.Add(i);

            CurrentPage = page.Page;
            TotalPages  = page.TotalPages;
            TotalCount  = page.TotalCount;
            IsEmpty     = Items.Count == 0;
        }
        catch (Exception ex)
        {
            SetError($"Could not load items: {ex.Message}");
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// @brief Appends the next page of results to the existing collection.
    /// @details No-op when there are no more pages or another load is in
    ///          flight. Wired to the explicit "Load more" button on the page.
    /// @return A task that completes once the next page is appended.
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsRefreshing || IsLoadingMore) return;
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
    /// @param item The item the user tapped. Tolerates null.
    [RelayCommand]
    public async Task SelectItemAsync(Item? item)
    {
        if (item is null) return;

        await _navigation.NavigateToAsync(
            "ItemDetailsPage",
            new Dictionary<string, object> { ["itemId"] = item.Id });
    }

    /// @brief Navigates to the create-item form.
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

    partial void OnTotalCountChanged(int value) =>
        OnPropertyChanged(nameof(RemainingCount));
}
