using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="ICategoryRepository"/>.
/// </summary>
public sealed class DbCategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public DbCategoryRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default)
        => await _db.Categories.ToListAsync(ct);

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Categories.FindAsync(new object[] { id }, ct);

    public async Task<Category> CreateAsync(Category entity, CancellationToken ct = default)
    {
        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Category> UpdateAsync(Category entity, CancellationToken ct = default)
    {
        _db.Categories.Update(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _db.Categories.FindAsync(new object[] { id }, ct);
        if (category is not null)
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync(ct);
        }
    }
}
