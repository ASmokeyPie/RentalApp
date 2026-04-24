using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentalApp.Database.Models;

/// <summary>
/// A registered user of the Library of Things platform.
/// Owns Items, borrows via Rentals, and writes Reviews.
/// </summary>
[Table("users")]
[PrimaryKey(nameof(Id))]
[Index(nameof(Email), IsUnique = true)]
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<Item> ItemsOwned { get; set; } = new();
    public List<Rental> RentalsAsBorrower { get; set; } = new();
    public List<Review> ReviewsWritten { get; set; } = new();

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}