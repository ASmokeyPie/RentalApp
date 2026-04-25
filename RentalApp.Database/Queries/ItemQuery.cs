namespace RentalApp.Database.Queries;

/// <summary>
/// Filter + paging options for <c>GET /items</c>. Mirrors the API query string
/// parameters one-for-one. Defaulted so callers can pass <c>new()</c> for an
/// unfiltered first page.
/// </summary>
/// <remarks>
/// All fields are optional. <see cref="CategorySlug"/> matches the API's
/// <c>?category=</c> parameter (slug, not id). Empty/whitespace strings are
/// treated as "not set" by the repository.
/// </remarks>
public sealed record ItemQuery
{
    /// <summary>Category slug filter (e.g. "power-tools"). Null = all categories.</summary>
    public string? CategorySlug { get; init; }

    /// <summary>Free-text search across title/description. Null = no search.</summary>
    public string? Search { get; init; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page. Defaults to 20. API caps at 100.</summary>
    public int PageSize { get; init; } = 20;
}
