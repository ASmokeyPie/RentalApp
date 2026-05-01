using RentalApp.Database.Models;

namespace RentalApp.Database.Helpers;

/// <summary>
/// Shared helper for rental-status derivation used by both the API and DB
/// repository implementations.
/// </summary>
public static class RentalStatusHelper
{
    /// <summary>
    /// Elevates <c>OutForRent</c> to <c>Overdue</c> when the rental's end date
    /// has passed. All other statuses are returned as-is. This is the only
    /// place <c>Overdue</c> is produced — the server/database never stores it.
    /// </summary>
    public static RentalStatus DeriveStatus(RentalStatus stored, DateOnly endDate)
    {
        if (stored == RentalStatus.OutForRent)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (endDate < today)
                return RentalStatus.Overdue;
        }
        return stored;
    }
}
