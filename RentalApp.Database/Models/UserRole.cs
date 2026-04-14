using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentalApp.Database.Models;

[Table("user_role")]
[PrimaryKey(nameof(Id))]
public class UserRole
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public int RoleId { get; set; }

    // Navigation properties with proper foreign key attributes
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
    
    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;
    
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public UserRole()
    {
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsActive = true;
    }
    
    public UserRole(int userId, int roleId)
    {
        UserId = userId;
        RoleId = roleId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsActive = true;
    }
    
    public void UpdateTimestamps()
    {
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void MarkAsDeleted()
    {
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
    }
    
    public void Restore()
    {
        DeletedAt = null;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public override string ToString()
    {
        return $"UserRole(Id: {Id}, UserId: {UserId}, RoleId: {RoleId}, CreatedAt: {CreatedAt}, UpdatedAt: {UpdatedAt}, DeletedAt: {DeletedAt}, IsActive: {IsActive})";
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is UserRole other)
        {
            return Id == other.Id &&
                   UserId == other.UserId &&
                   RoleId == other.RoleId &&
                   CreatedAt == other.CreatedAt &&
                   UpdatedAt == other.UpdatedAt &&
                   DeletedAt == other.DeletedAt &&
                   IsActive == other.IsActive;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, UserId, RoleId, CreatedAt, UpdatedAt, DeletedAt, IsActive);
    }
}