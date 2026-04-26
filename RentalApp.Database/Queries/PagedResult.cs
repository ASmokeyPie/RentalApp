namespace RentalApp.Database.Queries;

/// <summary>
/// Generic paginated result envelope. Mirrors the shape the hosted API returns
/// for paginated list endpoints (`{ items, page, pageSize, totalCount }`),
/// and provides a uniform return type for any local DB-backed paginated query
/// that the Db* repositories will eventually implement.
/// </summary>
public sealed class PagedResult<T>
{
    /// <summary>The items on this page (never null).</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of items requested per page.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Total number of pages given <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
    public int TotalPages =>
        PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>True if there is at least one page before this one.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>True if there is at least one page after this one.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>An empty page (no items, given page metadata preserved).</summary>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new() { Page = page, PageSize = pageSize, TotalCount = 0 };
}
