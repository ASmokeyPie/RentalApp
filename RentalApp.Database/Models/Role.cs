using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace RentalApp.Database.Models;

[Table("role")]
[PrimaryKey(nameof(Id))]
public class Role
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;

    // Navigation property - fixed naming
    public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
}