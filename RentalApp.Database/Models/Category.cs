using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RentalApp.Database.Models;

/// <summary>
/// Represents a category for organising rentable items
/// (e.g., "Power Tools", "Camping Gear", "Electronics")
/// </summary>
[Table("categories")]
[PrimaryKey(nameof(Id))]
[Index(nameof(Slug), IsUnique = true)]
public class Category
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Human-readable category name (e.g., "Power Tools")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly identifier (e.g., "power-tools"). Unique.
    /// Used in query strings like GET /items?category=power-tools
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property: All items in this category
    /// </summary>
    public List<Item> Items { get; set; } = new List<Item>();

    /// <summary>
    /// Computed count of items in this category.
    /// Exposed by the API as "itemCount" — not stored in the database.
    /// </summary>
    [NotMapped]
    public int ItemCount => Items?.Count ?? 0;
}