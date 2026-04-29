/// @file WriteReviewViewModel.cs
/// @brief Submit-review form view model
/// @author RentalApp Development Team
/// @date 2026

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model backing the leave-a-review form.
/// @details Reached from <c>RentalDetailsPage</c> via Shell route with a
///          <c>rentalId</c> query param. Loads the rental for display +
///          reviewability check, lets the user pick 1–5 stars and write an
///          optional comment, and submits via <see cref="IReviewService"/>.
///          The service enforces the business rules (Completed status,
///          borrower-only, rating range, comment length); the server enforces
///          "no duplicate review per rental" and surfaces 409 via the error
///          banner if the user already reviewed this rental.
///          MAUI-free for testability.
/// @extends BaseViewModel
public partial class WriteReviewViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IRentalService _rentals = null!;
    private readonly IReviewService _reviews = null!;
    private readonly IAuthenticationService? _auth;
    private readonly INavigationService _navigation = null!;

    /// @brief Set by the page from the Shell route parameter.
    [ObservableProperty]
    private int rentalId;

    /// @brief The rental being reviewed (used for read-only display).
    [ObservableProperty]
    private Rental? rental;

    /// @brief True once the rental loaded successfully AND the current user
    ///        is allowed to review it (Completed + borrower).
    [ObservableProperty]
    private bool canReview;

    /// @brief Star rating, 1–5. Default 5 (the most common rating + makes it
    ///        clear the picker is interactive without forcing a 0 state).
    [ObservableProperty]
    private int rating = 5;

    [ObservableProperty]
    private string comment = string.Empty;

    /// @brief True while the RefreshView spinner should be active.
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief Per-star symbol used by the picker buttons. ★ for "filled"
    ///        (rating ≥ this index), ☆ for "empty". Computed from
    ///        <see cref="Rating"/>; <see cref="OnRatingChanged"/> below
    ///        re-emits PropertyChanged for each so the bindings refresh.
    public string Star1Symbol => Rating >= 1 ? "★" : "☆";
    public string Star2Symbol => Rating >= 2 ? "★" : "☆";
    public string Star3Symbol => Rating >= 3 ? "★" : "☆";
    public string Star4Symbol => Rating >= 4 ? "★" : "☆";
    public string Star5Symbol => Rating >= 5 ? "★" : "☆";

    /// @brief Default constructor for design-time support.
    public WriteReviewViewModel()
    {
        Title = "Leave a review";
    }

    public WriteReviewViewModel(
        IRentalService rentals,
        IReviewService reviews,
        IAuthenticationService auth,
        INavigationService navigation)
    {
        _rentals = rentals;
        _reviews = reviews;
        _auth = auth;
        _navigation = navigation;
        Title = "Leave a review";
    }

    /// @brief Loads the rental and checks reviewability.
    /// @details Triggered by <see cref="OnRentalIdChanged"/> when Shell sets
    ///          the route parameter. Also wired to pull-to-refresh.
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (RentalId <= 0) return;
        // No early-return on IsRefreshing — RefreshView pre-toggles it.

        try
        {
            IsRefreshing = true;
            ClearError();

            var loaded = await _rentals.GetRentalAsync(RentalId);
            if (loaded is null)
            {
                Rental = null;
                CanReview = false;
                SetError($"Rental {RentalId} could not be found.");
                return;
            }

            Rental = loaded;
            var currentUserId = _auth?.CurrentUser?.Id ?? 0;
            CanReview = _reviews.IsRentalReviewable(loaded, currentUserId);

            if (!CanReview)
            {
                if (loaded.Status != RentalStatus.Completed)
                {
                    SetError("You can only review a rental once it has been marked Completed.");
                }
                else if (loaded.BorrowerId != currentUserId)
                {
                    SetError("Only the borrower can review this rental.");
                }
            }
            Title = string.IsNullOrWhiteSpace(loaded.ItemTitle)
                ? "Leave a review"
                : $"Review: {loaded.ItemTitle}";
        }
        catch (Exception ex)
        {
            Rental = null;
            CanReview = false;
            SetError($"Could not load rental: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// @brief Submits the review via the service.
    /// @details On success, navigates back to the rental detail page so the
    ///          user can confirm. On failure (validation OR server error such
    ///          as 409 already-reviewed), surfaces the message in the error
    ///          banner.
    [RelayCommand]
    public async Task SubmitAsync()
    {
        if (IsBusy) return;
        if (Rental is null || !CanReview)
        {
            SetError("You can't review this rental.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var currentUserId = _auth?.CurrentUser?.Id ?? 0;
            await _reviews.SubmitReviewAsync(
                Rental,
                Rating,
                string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim(),
                currentUserId);

            await _navigation.NavigateBackAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Service-level validation. Friendly message already.
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            // Server / network errors. 409 (already reviewed) flows through here.
            SetError($"Could not submit review: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// @brief Sets the rating to the supplied value. Bound to the five star
    ///        buttons in the picker.
    /// @details Takes a <see cref="string"/> rather than <see cref="int"/>
    ///          because XAML <c>CommandParameter="1"</c> serialises as a
    ///          string and CommunityToolkit's <c>RelayCommand&lt;int&gt;</c>
    ///          throws when it tries to cast — taking string here and parsing
    ///          locally is the simplest XAML-friendly fix.
    [RelayCommand]
    public void SetRating(string value)
    {
        if (int.TryParse(value, out var parsed) && parsed is >= 1 and <= 5)
        {
            Rating = parsed;
        }
    }

    // ---- Property change hooks --------------------------------------------

    partial void OnRentalIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadAsync();
        }
    }

    partial void OnRatingChanged(int value)
    {
        OnPropertyChanged(nameof(Star1Symbol));
        OnPropertyChanged(nameof(Star2Symbol));
        OnPropertyChanged(nameof(Star3Symbol));
        OnPropertyChanged(nameof(Star4Symbol));
        OnPropertyChanged(nameof(Star5Symbol));
    }
}
