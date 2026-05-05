using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RentalApp.Database.Models;

/// <summary>
/// A rentable item listed by an owner (e.g. a drill, a tent, a camera).
/// </summary>
[Table("items")]
[PrimaryKey(nameof(Id))]
public class Item
{
    public int Id { get; set; }

    /// <summary>Short, human-readable title (5–100 chars).</summary>
    [Required, MinLength(5), MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional long-form description (up to 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Rental price per day. 0 &lt; DailyRate ≤ 1000.</summary>
    [Required]
    [Range(0.01, 1000.00)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal DailyRate { get; set; }

    /// <summary>Foreign key to Category. Required.</summary>
    [Required]
    public int CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }

    /// <summary>Foreign key to the owning User. Required.</summary>
    [Required]
    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    /// <summary>Latitude in decimal degrees, -90 to 90.</summary>
    [Required]
    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    /// <summary>Longitude in decimal degrees, -180 to 180.</summary>
    [Required]
    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    /// <summary>Whether the item is currently listed as available to rent.</summary>
    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// PostGIS geography point used for spatial proximity queries
    /// (<c>ST_DWithin</c>). Populated from <see cref="Latitude"/> /
    /// <see cref="Longitude"/> on save.
    /// </summary>
    public Point? Location { get; set; }

    // Navigation properties
    public List<Rental> Rentals { get; set; } = new();

    /// <summary>
    /// Preview of description for list views (API returns full description,
    /// this is a client-side convenience).
    /// </summary>
    [NotMapped]
    public string? DescriptionPreview =>
        Description is null ? null
        : Description.Length > 100 ? Description[..100] + "..."
        : Description;

    // ---- Display-only fields populated by the API repository --------------
    // These mirror denormalised fields the API returns alongside an Item but
    // that aren't stored on the items table. Marked [NotMapped] so EF Core
    // ignores them. Setters exist so the API mapper can populate them; they
    // default to safe values for cases where the source isn't a wire payload
    // (e.g. constructed in tests, fetched from the local DB).

    /// <summary>Display name of the owner (e.g. "Ada L."). API only.</summary>
    [NotMapped]
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Owner's average review rating across all their items, 1–5.</summary>
    [NotMapped]
    public double? OwnerRating { get; set; }

    /// <summary>Average rating for THIS item across its reviews, 1–5.</summary>
    [NotMapped]
    public double? AverageRating { get; set; }

    /// <summary>Human-readable category name (e.g. "Power Tools").</summary>
    [NotMapped]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>URL slug of the category (e.g. "power-tools").</summary>
    [NotMapped]
    public string CategorySlug { get; set; } = string.Empty;

    /// <summary>
    /// Distance in kilometres from the query origin. Only populated by
    /// <c>GET /items/nearby</c> responses; null otherwise.
    /// </summary>
    [NotMapped]
    public double? DistanceKm { get; set; }

    /// <summary>
    /// Inline reviews surfaced by <c>GET /items/{id}</c>. Empty when the item
    /// is loaded from a list endpoint that doesn't include them, or when the
    /// item has no reviews yet. The dedicated paged endpoint
    /// <c>GET /items/{id}/reviews</c> can be used for a fuller list when
    /// <see cref="TotalReviews"/> exceeds what's inline here.
    /// </summary>
    [NotMapped]
    public List<Review> Reviews { get; set; } = new();

    /// <summary>
    /// Total number of reviews on this item across all pages. Reported by
    /// <c>GET /items/{id}</c> alongside the inline reviews.
    /// </summary>
    [NotMapped]
    public int TotalReviews { get; set; }
}