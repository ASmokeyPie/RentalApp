using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentalApp.Database.Models;

[Table("users")]
[PrimaryKey(nameof(Id))]
public class User
{
    public int Id { get; set; }
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    [Required]
    public string PasswordSalt { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
    
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}