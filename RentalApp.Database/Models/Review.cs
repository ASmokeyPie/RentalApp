using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RentalApp.Database.Models;

/// <summary>
/// A review left by a borrower against a completed Rental.
/// One review per rental (unique on RentalId).
/// </summary>
[Table("reviews")]
[PrimaryKey(nameof(Id))]
[Index(nameof(RentalId), IsUnique = true)]
public class Review
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Rental being reviewed
    /// </summary>
    [Required]
    public int RentalId { get; set; }

    /// <summary>
    /// Navigation property: The rental this review is for
    /// </summary>
    [ForeignKey(nameof(RentalId))]
    public Rental? Rental { get; set; }

    /// <summary>
    /// Foreign key to the User who wrote the review (the borrower on the rental).
    /// Duplicated here so queries like GET /users/{id}/reviews can join directly
    /// without going through Rental.
    /// </summary>
    [Required]
    public int ReviewerId { get; set; }

    /// <summary>
    /// Navigation property: The user who wrote the review
    /// </summary>
    [ForeignKey(nameof(ReviewerId))]
    public User? Reviewer { get; set; }

    /// <summary>
    /// Star rating from 1 to 5
    /// </summary>
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    /// <summary>
    /// Optional free-text comment (up to 500 chars)
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }

    /// <summary>
    /// When the review was submitted
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Display-only fields populated by the API repository --------------
    // The API surfaces reviews with denormalised reviewer + item info so list
    // views can render without further fetches. Marked [NotMapped] so EF Core
    // ignores them.

    /// <summary>Display name of the reviewer (e.g. "Ada L.").</summary>
    [NotMapped]
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>Title of the item that was rented and reviewed.</summary>
    [NotMapped]
    public string ItemTitle { get; set; } = string.Empty;
}