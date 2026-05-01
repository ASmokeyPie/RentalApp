using Microsoft.EntityFrameworkCore;
using RentalApp.Database.Data;
using RentalApp.Database.Models;

namespace RentalApp.Database.Repositories.Db;

/// <summary>
/// EF Core / local PostgreSQL implementation of <see cref="ICategoryRepository"/>.
/// Each method opens its own short-lived <see cref="AppDbContext"/> via the
/// factory so concurrent calls from different repositories never share a
/// context instance.
/// </summary>
public sealed class DbCategoryRepository : ICategoryRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DbCategoryRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        return await db.Categories.ToListAsync(ct);
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        return await db.Categories.FindAsync(new object[] { id }, ct);
    }

    public async Task<Category> CreateAsync(Category entity, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        db.Categories.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Category> UpdateAsync(Category entity, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        db.Categories.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        using var db = _factory.CreateDbContext();
        var category = await db.Categories.FindAsync(new object[] { id }, ct);
        if (category is not null)
        {
            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);
        }
    }
}
