using System.Net.Http.Json;
using RentalApp.Database.Models;

namespace RentalApp.Database.Repositories.Api;

/// <summary>
/// <see cref="ICategoryRepository"/> backed by the hosted API. The API only
/// exposes <c>GET /categories</c>; all mutating CRUD operations throw
/// <see cref="NotSupportedException"/>.
/// </summary>
public sealed class ApiCategoryRepository : ICategoryRepository
{
    private readonly HttpClient _http;

    public ApiCategoryRepository(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default)
    {
        // Spec wraps the array: { "categories": [...] }
        var wire = await _http.GetFromJsonAsync<CategoriesEnvelope>(
            "categories", ApiJsonOptions.Default, ct);
        return (wire?.Categories ?? Array.Empty<CategoryWire>()).Select(ToModel).ToList();
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // The API has no GET /categories/{id}; resolve client-side from the list.
        var all = await ListAsync(ct);
        return all.FirstOrDefault(c => c.Id == id);
    }

    public Task<Category> CreateAsync(Category entity, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose POST /categories.");

    public Task<Category> UpdateAsync(Category entity, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose PUT /categories/{id}.");

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose DELETE /categories/{id}.");

    // ---- Wire record + mapper ---------------------------------------------

    private static Category ToModel(CategoryWire w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Slug = w.Slug,
        // ItemCount on the model is computed from Items.Count; the API returns
        // its own count which we don't surface (Items list is empty here).
    };

    private sealed record CategoryWire(int Id, string Name, string Slug, int? ItemCount);

    private sealed record CategoriesEnvelope(IReadOnlyList<CategoryWire>? Categories);
}
