namespace RentalApp.Database.Repositories;

/// <summary>
/// Generic repository contract for entities with an integer primary key.
/// Specialised resource repositories (<c>IItemRepository</c>, <c>IRentalRepository</c>,
/// etc.) extend this interface and add endpoint-specific methods on top.
/// </summary>
/// <remarks>
/// Some implementations may not be able to satisfy every method — e.g. the
/// hosted API has no <c>DELETE /rentals/{id}</c>. In that case the
/// implementation should throw <see cref="NotSupportedException"/> with a
/// clear explanation. ViewModels and services should not catch
/// <see cref="NotSupportedException"/> to recover; they should simply not
/// call operations they know aren't supported by the active backend.
/// </remarks>
public interface IRepository<T> where T : class
{
    /// <summary>Fetch a single entity by its primary key, or <c>null</c> if not found.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Fetch all entities. Prefer paginated specialised methods for large sets.</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    /// <summary>Persist a new entity and return the server-assigned representation (with id, timestamps, etc.).</summary>
    Task<T> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>Persist updates to an existing entity and return the new representation.</summary>
    Task<T> UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>Delete an entity by its primary key. No-op semantics if not found are implementation-defined.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}
