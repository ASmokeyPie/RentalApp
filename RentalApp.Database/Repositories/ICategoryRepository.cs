using RentalApp.Database.Models;

namespace RentalApp.Database.Repositories;

/// <summary>
/// Data access for <see cref="Category"/>. The hosted API only exposes
/// <c>GET /categories</c> (read-only) — there is no public create/update/delete.
/// Mutating CRUD methods on the API implementation therefore throw
/// <see cref="NotSupportedException"/>; the eventual local-DB implementation
/// can fill them in.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
}
