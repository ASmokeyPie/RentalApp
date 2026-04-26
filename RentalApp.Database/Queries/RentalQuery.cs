using RentalApp.Database.Models;

namespace RentalApp.Database.Queries;

/// <summary>
/// Filter options for <c>GET /rentals/incoming</c> and <c>GET /rentals/outgoing</c>.
/// The API supports a single optional <c>?status=</c> filter; this record
/// is the strongly-typed equivalent.
/// </summary>
public sealed record RentalQuery
{
    /// <summary>Filter by current rental status. Null = all statuses.</summary>
    public RentalStatus? Status { get; init; }
}
