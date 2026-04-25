using RentalApp.Database.Models;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="ICategoryRepository"/>.
/// Stubbed — see remarks on <see cref="DbItemRepository"/>.
/// </summary>
public sealed class DbCategoryRepository : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Category> CreateAsync(Category entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Category> UpdateAsync(Category entity, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
