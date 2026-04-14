using RentalApp.Database.Models;

namespace RentalApp.Services;

public class AuthStateChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; set; }
    public User? User { get; set; }
    public List<string> Roles { get; set; } = new();
}