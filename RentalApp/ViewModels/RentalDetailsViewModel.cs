/// @file RentalDetailsViewModel.cs
/// @brief Rental detail + workflow-action view model
/// @author RentalApp Development Team
/// @date 2026

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;
using RentalApp.Services;

namespace RentalApp.ViewModels;

/// @brief View model for the rental-detail page.
/// @details Loads a rental by id and surfaces the workflow-action commands the
///          viewer is permitted to invoke, given their role (owner vs borrower)
///          and the rental's current status. Commands delegate to
///          <see cref="IRentalService"/>; the state-machine legality table
///          lives there, and the server is the ultimate authority (409 on
///          illegal transitions).
///
///          Action visibility (drives button IsVisible bindings):
///          <list type="bullet">
///            <item><description><b>Owner</b> may Approve or Reject a Requested
///              rental, MarkOutForRent an Approved one, MarkReturned an
///              OutForRent one, MarkCompleted a Returned one.</description></item>
///            <item><description><b>Borrower</b> may Cancel a Requested or
///              Approved rental.</description></item>
///          </list>
///          MAUI-free for testability.
/// @extends BaseViewModel
public partial class RentalDetailsViewModel : BaseViewModel
{
    // null! suppresses CS8618 for the design-time parameterless ctor; the
    // runtime DI ctor always assigns these before any command runs.
    private readonly IRentalService _rentals = null!;
    private readonly IAuthenticationService? _auth;
    private readonly INavigationService? _navigation;

    /// @brief Set by the page from the Shell route parameter.
    [ObservableProperty]
    private int rentalId;

    /// @brief The loaded rental, or null while loading / on error.
    [ObservableProperty]
    private Rental? rental;

    /// @brief True once the rental has loaded successfully.
    [ObservableProperty]
    private bool isLoaded;

    /// @brief True while the RefreshView spinner should be active.
    [ObservableProperty]
    private bool isRefreshing;

    /// @brief True while an action command (Approve/Reject/etc.) is in flight.
    ///        Drives action-button IsEnabled bindings to prevent double-taps.
    [ObservableProperty]
    private bool isWorking;

    // ---- Role / permission flags -----------------------------------------

    /// @brief Authenticated viewer is the owner of the rented item.
    public bool IsOwner =>
        Rental is not null
        && CurrentUserId is int uid
        && uid != 0
        && Rental.OwnerId == uid;

    /// @brief Authenticated viewer is the borrower on this rental.
    public bool IsBorrower =>
        Rental is not null
        && CurrentUserId is int uid
        && uid != 0
        && Rental.BorrowerId == uid;

    public bool CanApprove        => IsOwner    && Rental?.Status == RentalStatus.Requested;
    public bool CanReject         => IsOwner    && Rental?.Status == RentalStatus.Requested;
    public bool CanMarkOutForRent => IsOwner    && Rental?.Status == RentalStatus.Approved;
    // Borrower marks Returned per server rule "only the borrower can perform
    // this transition" (they're physically returning the item). Owner then
    // inspects and marks Completed below.
    public bool CanMarkReturned   => IsBorrower && Rental?.Status == RentalStatus.OutForRent;
    public bool CanMarkCompleted  => IsOwner    && Rental?.Status == RentalStatus.Returned;

    /// @brief Borrower can leave a review once the rental is Completed.
    /// @details The "no duplicate review" rule lives server-side; if the
    ///          borrower already reviewed this rental, tapping the button
    ///          will result in a 409 surfaced via the error banner.
    public bool CanLeaveReview    => IsBorrower && Rental?.Status == RentalStatus.Completed;

    /// @brief True when at least one action button should appear.
    public bool HasAnyAction =>
        CanApprove || CanReject || CanMarkOutForRent || CanMarkReturned || CanMarkCompleted || CanLeaveReview;

    private int? CurrentUserId => _auth?.CurrentUser?.Id;

    /// @brief Default constructor for design-time support.
    public RentalDetailsViewModel()
    {
        Title = "Rental";
    }

    public RentalDetailsViewModel(
        IRentalService rentals,
        IAuthenticationService auth,
        INavigationService navigation)
    {
        _rentals = rentals;
        _auth = auth;
        _navigation = navigation;
        Title = "Rental";
    }

    /// @brief Loads the rental by id.
    /// @details Triggered automatically by <see cref="OnRentalIdChanged"/> when
    ///          Shell sets the route parameter; also wired to pull-to-refresh.
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
                IsLoaded = false;
                SetError($"Rental {RentalId} could not be found.");
                Title = "Rental";
                NotifyDerivedFlags();
                return;
            }

            Rental = loaded;
            IsLoaded = true;
            Title = string.IsNullOrWhiteSpace(loaded.ItemTitle) ? "Rental" : $"Rental: {loaded.ItemTitle}";
            NotifyDerivedFlags();
        }
        catch (Exception ex)
        {
            Rental = null;
            IsLoaded = false;
            SetError($"Could not load rental: {ex.Message}");
            NotifyDerivedFlags();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    // ---- Action commands --------------------------------------------------

    [RelayCommand]
    public Task ApproveAsync() =>
        InvokeActionAsync(svc => svc.ApproveAsync(Rental!.Id, Rental!.Status));

    [RelayCommand]
    public Task RejectAsync() =>
        InvokeActionAsync(svc => svc.RejectAsync(Rental!.Id, Rental!.Status));

    [RelayCommand]
    public Task MarkOutForRentAsync() =>
        InvokeActionAsync(svc => svc.MarkOutForRentAsync(Rental!.Id, Rental!.Status));

    [RelayCommand]
    public Task MarkReturnedAsync() =>
        InvokeActionAsync(svc => svc.MarkReturnedAsync(Rental!.Id, Rental!.Status));

    [RelayCommand]
    public Task MarkCompletedAsync() =>
        InvokeActionAsync(svc => svc.MarkCompletedAsync(Rental!.Id, Rental!.Status));

    /// @brief Navigates to the leave-a-review form for this rental.
    /// @details Borrower-only — guarded by <see cref="CanLeaveReview"/> at the
    ///          binding level (button hidden when false) and again here as a
    ///          defensive check. The actual submission happens on
    ///          <c>WriteReviewPage</c> via <see cref="IReviewService"/>.
    [RelayCommand]
    public async Task LeaveReviewAsync()
    {
        if (!CanLeaveReview || Rental is null) return;
        if (_navigation is null) return;

        await _navigation.NavigateToAsync(
            "WriteReviewPage",
            new Dictionary<string, object> { ["rentalId"] = Rental.Id });
    }

    /// @brief Shared shell for action commands. Guards against double-taps
    ///        and missing rental, surfaces errors, and patches the in-memory
    ///        rental's Status from the returned <see cref="RentalStatusUpdate"/>
    ///        so the UI re-evaluates the Can* flags without a round-trip.
    private async Task InvokeActionAsync(Func<IRentalService, Task<RentalStatusUpdate>> action)
    {
        if (Rental is null) return;
        if (IsWorking) return;

        try
        {
            IsWorking = true;
            ClearError();

            var update = await action(_rentals);

            // Patch the in-memory rental so derived flags re-evaluate.
            Rental.Status = update.Status;
            Rental.UpdatedAt = update.UpdatedAt;

            // Trigger PropertyChanged for Rental so the page redraws and the
            // Can* derived properties are re-read by their bindings.
            OnPropertyChanged(nameof(Rental));
            NotifyDerivedFlags();
        }
        catch (Exception ex)
        {
            SetError($"Could not complete action: {ex.Message}");
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// @brief Re-emits PropertyChanged for every computed permission/role
    ///        property so XAML bindings refresh after a load or action.
    private void NotifyDerivedFlags()
    {
        OnPropertyChanged(nameof(IsOwner));
        OnPropertyChanged(nameof(IsBorrower));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanReject));
        OnPropertyChanged(nameof(CanMarkOutForRent));
        OnPropertyChanged(nameof(CanMarkReturned));
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanLeaveReview));
        OnPropertyChanged(nameof(HasAnyAction));
    }

    // ---- Property change hooks --------------------------------------------

    partial void OnRentalIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadAsync();
        }
    }
}
