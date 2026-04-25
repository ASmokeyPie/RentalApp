using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories;

/// <summary>
/// Data access for <see cref="Rental"/>. Wraps the rental-related API
/// endpoints. The hosted API has no DELETE rental, no UPDATE-the-whole-thing
/// (only <c>PATCH /rentals/{id}/status</c>), and a non-standard create shape
/// (<c>{itemId, startDate, endDate}</c> rather than a full Rental body), so
/// the specialised methods below carry all real call sites.
/// </summary>
public interface IRentalRepository : IRepository<Rental>
{
    /// <summary>Submit a new rental request. Server returns a populated <see cref="Rental"/>.</summary>
    Task<Rental> RequestAsync(int itemId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>Rentals where the current user is the OWNER of the item, optionally filtered by status.</summary>
    Task<IReadOnlyList<Rental>> GetIncomingAsync(RentalQuery? query = null, CancellationToken ct = default);

    /// <summary>Rentals where the current user is the BORROWER, optionally filtered by status.</summary>
    Task<IReadOnlyList<Rental>> GetOutgoingAsync(RentalQuery? query = null, CancellationToken ct = default);

    /// <summary>
    /// Move a rental to a new state via <c>PATCH /rentals/{id}/status</c>.
    /// Server enforces the state machine (409 on invalid transitions). Returns
    /// the slim <see cref="RentalStatusUpdate"/> shape the API actually echoes
    /// back (id, status, updatedAt) — not a full <see cref="Rental"/>. If the
    /// caller needs a refreshed Rental, follow up with <see cref="GetByIdAsync"/>.
    /// </summary>
    Task<RentalStatusUpdate> UpdateStatusAsync(int rentalId, RentalStatus newStatus, CancellationToken ct = default);
}
