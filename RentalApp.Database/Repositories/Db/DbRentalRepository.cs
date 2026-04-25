using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="IRentalRepository"/>.
/// Stubbed — see remarks on <see cref="DbItemRepository"/>.
/// </summary>
public sealed class DbRentalRepository : IRentalRepository
{
    public Task<Rental?> GetByIdAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Rental>> ListAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Rental> CreateAsync(Rental entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Rental> UpdateAsync(Rental entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Rental> RequestAsync(int itemId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Rental>> GetIncomingAsync(RentalQuery? query = null, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Rental>> GetOutgoingAsync(RentalQuery? query = null, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<RentalStatusUpdate> UpdateStatusAsync(int rentalId, RentalStatus newStatus, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
