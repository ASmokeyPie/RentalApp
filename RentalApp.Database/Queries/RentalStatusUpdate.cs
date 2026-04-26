using RentalApp.Database.Models;

namespace RentalApp.Database.Queries;

/// <summary>
/// Slim result of <c>PATCH /rentals/{id}/status</c>. The API echoes back only
/// the rental id, the new status, and the timestamp of the change — not the
/// full rental — so this record matches that shape rather than fabricating a
/// full <see cref="Rental"/>. Callers that need a refreshed full Rental should
/// follow up with <see cref="Repositories.IRentalRepository.GetByIdAsync"/>.
/// </summary>
public sealed record RentalStatusUpdate(int Id, RentalStatus Status, DateTime UpdatedAt);
