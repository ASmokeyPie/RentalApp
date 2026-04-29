/// @file ProfileViewModel.cs
/// @brief User profile view model — displays account info, average rating, and reviews written
/// @author RentalApp Development Team
/// @date 2026

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Repositories;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the user profile page.
/// @details Loads the authenticated user's account information and the reviews
///          they have written. Computes the user's average rating from the
///          fetched review set so it can be displayed as a summary stat.
///          Fetches reviews via <see cref="IReviewRepository.GetForUserAsync"/>
///          with a large page size so a single request covers most users.
///          MAUI-free for testability.
/// @extends BaseViewModel
public partial class ProfileViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IAuthenticationService _auth = null!;
    private readonly IReviewRepository _reviews = null!;
    private readonly INavigationService _navigation = null!;

    /// @brief Reviews written by the current user.
    public ObservableCollection<Review> Reviews { get; } = new();

    /// @brief The currently authenticated user. Populated on load.
    [ObservableProperty]
    private User? currentUser;

    /// @brief The user's average star rating across all their fetched reviews,
    ///        or null if they haven't written any.
    [ObservableProperty]
    private double? averageRating;

    /// @brief Total number of reviews the user has written (from the server's
    ///        TotalCount, which is authoritative even when multiple pages exist).
    [ObservableProperty]
    private int totalReviews;

    /// @brief True once the profile has loaded successfully at least once.
    [ObservableProperty]
    private bool isLoaded;

    /// @brief True while the RefreshView spinner should be active.
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief True when the user has written no reviews AND loading is complete.
    [ObservableProperty]
    private bool isEmpty;

    /// @brief Human-readable average rating for display (e.g. "4.3 ★" or
    ///        "No ratings yet"). Recomputed whenever AverageRating changes.
    public string AverageRatingDisplay =>
        AverageRating.HasValue ? $"{AverageRating.Value:F1} ★" : "No ratings yet";

    /// @brief Default constructor for design-time support.
    public ProfileViewModel()
    {
        Title = "Profile";
    }

    /// @brief Initializes a new instance of <see cref="ProfileViewModel"/>.
    /// @param auth Authentication service — supplies the current user and logout.
    /// @param reviews Review repository — supplies the user's written reviews.
    /// @param navigation Navigation service — used for the logout redirect.
    public ProfileViewModel(
        IAuthenticationService auth,
        IReviewRepository reviews,
        INavigationService navigation) : this()
    {
        _auth = auth;
        _reviews = reviews;
        _navigation = navigation;
    }

    /// @brief Loads (or reloads) the profile: fresh user info + their reviews.
    /// @details Wired as both the page's on-appearing handler and the
    ///          pull-to-refresh command. The sequence is:
    ///          1. Call <see cref="IAuthenticationService.RefreshCurrentUserAsync"/>
    ///             to re-fetch GET /users/me — this gives us the server-computed
    ///             <c>averageRating</c> (accurate regardless of review count).
    ///          2. Fetch the first page of reviews via
    ///             <see cref="IReviewRepository.GetForUserAsync"/> (pageSize 50,
    ///             the API's documented maximum for that endpoint).
    ///          Does NOT early-return on IsRefreshing — the RefreshView pre-sets
    ///          it before firing the command, so early-returning would leave the
    ///          spinner stuck (same pattern used by other VMs in this project).
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            ClearError();

            // Re-fetch /users/me so AverageRating reflects the latest server value.
            await _auth.RefreshCurrentUserAsync();
            CurrentUser = _auth.CurrentUser;

            if (CurrentUser is null)
            {
                SetError("You must be signed in to view your profile.");
                IsLoaded = false;
                return;
            }

            // Fetch the user's reviews. The API caps pageSize at 50 for this endpoint.
            var page = await _reviews.GetForUserAsync(CurrentUser.Id, page: 1, pageSize: 50);

            Reviews.Clear();
            foreach (var r in page.Items)
                Reviews.Add(r);

            // AverageRating comes from the refreshed CurrentUser (server-computed,
            // always accurate). TotalReviews comes from the paged result's TotalCount.
            AverageRating = CurrentUser.AverageRating;
            TotalReviews  = page.TotalCount;

            IsEmpty   = Reviews.Count == 0;
            IsLoaded  = true;
        }
        catch (Exception ex)
        {
            SetError($"Could not load profile: {ex.Message}");
            IsLoaded = false;
            IsEmpty  = Reviews.Count == 0;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// @brief Logs the user out and navigates back to the login page.
    [RelayCommand]
    public async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await _navigation.NavigateToAsync("LoginPage");
    }

    // ---- Property change hooks -------------------------------------------

    partial void OnAverageRatingChanged(double? value) =>
        OnPropertyChanged(nameof(AverageRatingDisplay));
}
