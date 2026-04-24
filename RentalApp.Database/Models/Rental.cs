using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RentalApp.Database.Models;

/// <summary>
/// Lifecycle states for a Rental. The API's PATCH /rentals/{id}/status endpoint
/// enforces a state machine — your service layer should validate legal transitions
/// (e.g. Pending → Approved → Active → Completed, Pending → Rejected, etc.).
/// Configure string persistence in DbContext:
///   modelBuilder.Entity&lt;Rental&gt;().Property(r => r.Status).HasConversion&lt;string&gt;();
/// </summary>
public enum RentalStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Active,
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
    public RentalStatus Status { get; set; } = RentalStatus.Pending;

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
}