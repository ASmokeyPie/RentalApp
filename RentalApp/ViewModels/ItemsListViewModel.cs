/// @file ItemsListViewModel.cs
/// @brief Browse-items list view model with pull-to-refresh, Load-more button, category filter, and text search
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

/// @brief A selectable entry in the category filter picker.
/// @details The null <see cref="Slug"/> value represents "All categories" —
///          i.e. no category filter is applied.
public sealed record CategoryFilterItem(string Name, string? Slug);

/// @brief View model for the paginated browse-items list.
/// @details Loads items via <see cref="IItemRepository.SearchAsync"/> one page
///          at a time. Pull-to-refresh resets to page 1 and replaces the
///          collection; an explicit <see cref="LoadMoreCommand"/> button
///          appends the next page (no infinite-scroll trigger — that proved
///          flaky in practice and conflated with the refresh state). Tapping
///          an item navigates to <c>ItemDetailsPage</c>.
///          A <see cref="SelectedCategoryFilter"/> property lets the user
///          narrow results to a single category; changing it auto-refreshes
///          the list from page 1.
///          MAUI-free so it can be unit-tested against the .NET 10 test
///          project; visual concerns (RefreshView, CollectionView) live in
///          the XAML page.
/// @extends BaseViewModel
public partial class ItemsListViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IItemRepository _items = null!;
    private readonly INavigationService _navigation = null!;
    private readonly ICategoryRepository? _categoriesRepo;

    /// @brief Items currently loaded into the list. Pages are appended in
    ///        load order. Page-level changes (CollectionChanged) refresh the
    ///        derived properties below.
    public ObservableCollection<Item> Items { get; }

    /// @brief Category filter options shown in the picker.
    /// @details The first entry is always "All Categories" (Slug = null).
    ///          Populated by <see cref="LoadCategoriesAsync"/> on first
    ///          appearance. Empty until that call completes.
    public ObservableCollection<CategoryFilterItem> CategoryFilters { get; }

    /// @brief The currently selected category filter (null = all categories).
    /// @details Changing this property triggers an automatic page-1 refresh
    ///          provided the list has already been loaded at least once
    ///          (<see cref="CurrentPage"/> &gt; 0), so the initial category
    ///          selection during <see cref="LoadCategoriesAsync"/> does not
    ///          cause a double-load.
    [ObservableProperty]
    private CategoryFilterItem? selectedCategoryFilter;

    /// @brief Free-text search term applied across item title and description.
    /// @details Changing this triggers an automatic page-1 refresh once the
    ///          list has been loaded at least once, matching the same guard
    ///          used by <see cref="SelectedCategoryFilter"/>.
    [ObservableProperty]
    private string searchText = string.Empty;

    /// @brief True while categories are being fetched for the filter picker.
    [ObservableProperty]
    private bool isLoadingCategories;

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
        CategoryFilters = new ObservableCollection<CategoryFilterItem>();
        Title = "Browse Items";
    }

    /// @brief Initialises a new instance of the <see cref="ItemsListViewModel"/> class.
    /// @param items       Item repository (API or DB-backed; the VM doesn't care).
    /// @param navigation  Navigation service used to push the detail page.
    /// @param categories  Optional category repository for the filter picker.
    ///                    When null (e.g. in unit tests) the picker simply shows
    ///                    no categories — all other behaviour is unaffected.
    public ItemsListViewModel(
        IItemRepository items,
        INavigationService navigation,
        ICategoryRepository? categories = null)
        : this()
    {
        _items = items;
        _navigation = navigation;
        _categoriesRepo = categories;
    }

    /// @brief Loads categories into <see cref="CategoryFilters"/> from the
    ///        repository, prepending an "All Categories" entry.
    /// @details Safe to call multiple times — returns immediately if the
    ///          collection is already populated or no repository is wired.
    ///          Category-load failures are swallowed: the browse page still
    ///          works, just without the filter picker populated.
    public async Task LoadCategoriesAsync()
    {
        if (_categoriesRepo is null || CategoryFilters.Count > 0) return;

        try
        {
            IsLoadingCategories = true;
            var all = await _categoriesRepo.ListAsync();

            CategoryFilters.Add(new CategoryFilterItem("All Categories", null));
            foreach (var c in all)
                CategoryFilters.Add(new CategoryFilterItem(c.Name, c.Slug));

            // Pre-select "All Categories" without triggering an auto-refresh
            // (CurrentPage is still 0 at this point, so the guard in
            // OnSelectedCategoryFilterChanged prevents the double-load).
            SelectedCategoryFilter = CategoryFilters[0];
        }
        catch
        {
            // Non-fatal: the list will work without the filter populated.
        }
        finally
        {
            IsLoadingCategories = false;
        }
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

            var page = await _items.SearchAsync(new ItemQuery
            {
                Page = 1,
                PageSize = PageSize,
                CategorySlug = SelectedCategoryFilter?.Slug,
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            });

            Items.Clear();
            foreach (var i in page.Items) Items.Add(i);

            CurrentPage = page.Page;
            TotalPages = page.TotalPages;
            TotalCount = page.TotalCount;
            IsEmpty = Items.Count == 0;
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
            var page = await _items.SearchAsync(new ItemQuery
            {
                Page = nextPage,
                PageSize = PageSize,
                CategorySlug = SelectedCategoryFilter?.Slug,
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            });

            foreach (var i in page.Items) Items.Add(i);

            CurrentPage = page.Page;
            TotalPages = page.TotalPages;
            TotalCount = page.TotalCount;
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

    /// @brief Triggers a page-1 refresh when the category filter changes —
    ///        but only after the list has already been loaded at least once.
    /// @details The guard on <see cref="CurrentPage"/> prevents a double-load
    ///          during the initial <see cref="LoadCategoriesAsync"/> call,
    ///          where the "All Categories" item is pre-selected before the
    ///          first <see cref="RefreshAsync"/> has run.
    partial void OnSelectedCategoryFilterChanged(CategoryFilterItem? value)
    {
        if (CurrentPage > 0)
            _ = RefreshAsync();
    }

    /// @brief Triggers a page-1 refresh when the search text changes —
    ///        same guard as category: only after the first load has run.
    partial void OnSearchTextChanged(string value)
    {
        if (CurrentPage > 0)
            _ = RefreshAsync();
    }
}
