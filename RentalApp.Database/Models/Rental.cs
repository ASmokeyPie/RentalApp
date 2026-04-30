using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RentalApp.Database.Models;

/// <summary>
/// Lifecycle states for a Rental, matching the
/// hosted API's state machine:
/// <list type="bullet">
///   <item><description><c>Requested</c> → <c>Approved</c> | <c>Rejected</c> (owner decides).</description></item>
///   <item><description><c>Approved</c> → <c>OutForRent</c> (owner marks the item out at pickup).</description></item>
///   <item><description><c>OutForRent</c> → <c>Returned</c> (borrower records the item is back).</description></item>
///   <item><description><c>Returned</c> → <c>Completed</c> (owner closes out the rental).</description></item>
///   <item><description><c>Rejected</c>, <c>Completed</c> are terminal.</description></item>
/// </list>
/// The full transition table lives in <c>RentalApp.Services.RentalService</c>.
/// The API's PATCH /rentals/{id}/status enforces the same rules server-side
/// (returns 409 on illegal transitions).
/// 
/// Configure string persistence in DbContext:
///   modelBuilder.Entity&lt;Rental&gt;().Property(r => r.Status).HasConversion&lt;string&gt;();
/// </summary>
public enum RentalStatus
{
    Requested,
    Approved,
    Rejected,
    OutForRent,
    /// <summary>
    /// Client-side derived state: OutForRent rental whose EndDate has passed.
    /// Never sent to the API — the server always sees "Out for Rent".
    /// </summary>
    Overdue,
    Returned,
    Completed
}

/// <summary>
/// A rental booking — a borrower requests an Item for a date range.
/// </summary>
[Table("rentals")]
[PrimaryKey(nameof(Id))]
public class Rental
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Item being rented
    /// </summary>
    [Required]
    public int ItemId { get; set; }

    /// <summary>
    /// Navigation property: The item being rented.
    /// Owner is reached via Item.Owner (no separate OwnerId stored on Rental).
    /// </summary>
    [ForeignKey(nameof(ItemId))]
    public Item? Item { get; set; }

    /// <summary>
    /// Foreign key to the User borrowing the item
    /// </summary>
    [Required]
    public int BorrowerId { get; set; }

    /// <summary>
    /// Navigation property: The user renting the item
    /// </summary>
    [ForeignKey(nameof(BorrowerId))]
    public User? Borrower { get; set; }

    /// <summary>
    /// First day of the rental (inclusive)
    /// </summary>
    [Required]
    [Column(TypeName = "date")]
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Last day of the rental (inclusive)
    /// </summary>
    [Required]
    [Column(TypeName = "date")]
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Current status in the rental lifecycle
    /// </summary>
    [Required]
    public RentalStatus Status { get; set; } = RentalStatus.Requested;

    /// <summary>
    /// Snapshot of total price at creation time (DailyRate × duration in days).
    /// Stored so historical rentals aren't affected by future price changes.
    /// </summary>
    [Required]
    [Column(TypeName = "numeric(10,2)")]
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// When the rental was requested.
    /// Maps to the API's "createdAt" and "requestedAt" fields (same value).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the rental was last modified (e.g. status transitions)
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property: The review left for this rental, if any.
    /// One review per rental (enforced by unique index on Review.RentalId).
    /// </summary>
    public Review? Review { get; set; }

    /// <summary>
    /// Computed: number of days the rental covers (used for price calculations)
    /// </summary>
    [NotMapped]
    public int DurationDays => EndDate.DayNumber - StartDate.DayNumber;

    // ---- Display-only fields populated by the API repository --------------
    // The API returns denormalised fields (item title, owner name, etc.)
    // alongside a rental so list views don't need a follow-up Item fetch.
    // Marked [NotMapped] so EF Core ignores them.

    /// <summary>Title of the rented item (display).</summary>
    [NotMapped]
    public string ItemTitle { get; set; } = string.Empty;

    /// <summary>Daily rate of the item at the time the rental was created.</summary>
    [NotMapped]
    public decimal? ItemDailyRate { get; set; }

    /// <summary>Display name of the borrower (e.g. "Ada L.").</summary>
    [NotMapped]
    public string BorrowerName { get; set; } = string.Empty;

    /// <summary>Item owner's id, surfaced from the rental wire response so
    /// callers don't need to load <see cref="Item"/> separately to know
    /// who owns it. Zero when the wire didn't populate it.</summary>
    [NotMapped]
    public int OwnerId { get; set; }

    /// <summary>Display name of the owner (the item's owner).</summary>
    [NotMapped]
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>True if today falls within [StartDate, EndDate] inclusive.</summary>
    [NotMapped]
    public bool IsActiveOnToday
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return today >= StartDate && today <= EndDate;
        }
    }
}