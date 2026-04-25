using System.Globalization;
using System.Net.Http.Json;
using RentalApp.Database.Models;
using RentalApp.Database.Queries;

namespace RentalApp.Database.Repositories.Api;

/// <summary>
/// <see cref="IRentalRepository"/> backed by the hosted API.
/// </summary>
/// <remarks>
/// Wire-shape quirks worth knowing:
/// <list type="bullet">
///   <item><description>POST <c>/rentals</c> response uses <c>createdAt</c>; GET <c>/rentals/{id}</c> uses <c>requestedAt</c> (and adds <c>itemDescription</c>).</description></item>
///   <item><description>GET <c>/rentals/incoming</c> and <c>/outgoing</c> wrap the array in <c>{ rentals, totalRentals }</c>.</description></item>
///   <item><description>PATCH <c>/rentals/{id}/status</c> returns only <c>{ id, status, updatedAt }</c>.</description></item>
///   <item><description>The status field is bare <c>string</c> on the wire. We parse it into <see cref="RentalStatus"/> defensively; an unknown string throws <see cref="InvalidDataException"/> with the offending value so Phase 5 work can pin the canonical strings.</description></item>
/// </list>
/// Date strings are <c>"yyyy-MM-dd"</c> on the wire and parse to <see cref="DateOnly"/> via <see cref="DateOnly.ParseExact(string,string,IFormatProvider)"/> with invariant culture.
/// </remarks>
public sealed class ApiRentalRepository : IRentalRepository
{
    private readonly HttpClient _http;

    public ApiRentalRepository(HttpClient http) => _http = http;

    // ---- Generic CRUD -----------------------------------------------------

    public async Task<Rental?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"rentals/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var wire = await response.Content.ReadFromJsonAsync<RentalDetailWire>(ApiJsonOptions.Default, ct);
        return wire is null ? null : ToModel(wire);
    }

    public Task<IReadOnlyList<Rental>> ListAsync(CancellationToken ct = default) =>
        throw new NotSupportedException(
            "The API has no flat /rentals list — call GetIncomingAsync or GetOutgoingAsync.");

    public Task<Rental> CreateAsync(Rental entity, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Use RequestAsync(itemId, startDate, endDate); a Rental cannot be POSTed wholesale.");

    public Task<Rental> UpdateAsync(Rental entity, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Use UpdateStatusAsync; the API has no PUT /rentals/{id}.");

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted API does not expose DELETE /rentals/{id}.");

    // ---- Specialised methods ----------------------------------------------

    public async Task<Rental> RequestAsync(int itemId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var body = new CreateRentalBody(
            ItemId: itemId,
            StartDate: startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate: endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var response = await _http.PostAsJsonAsync("rentals", body, ApiJsonOptions.Default, ct);
        response.EnsureSuccessStatusCode();
        var wire = await response.Content.ReadFromJsonAsync<RentalSummaryWire>(ApiJsonOptions.Default, ct)
                   ?? throw new InvalidOperationException("Empty response body from POST /rentals.");
        return ToModel(wire);
    }

    public Task<IReadOnlyList<Rental>> GetIncomingAsync(RentalQuery? query = null, CancellationToken ct = default) =>
        GetRentalsAsync("rentals/incoming", query, ct);

    public Task<IReadOnlyList<Rental>> GetOutgoingAsync(RentalQuery? query = null, CancellationToken ct = default) =>
        GetRentalsAsync("rentals/outgoing", query, ct);

    public async Task<RentalStatusUpdate> UpdateStatusAsync(int rentalId, RentalStatus newStatus, CancellationToken ct = default)
    {
        var body = new UpdateStatusBody(newStatus.ToString());
        var request = new HttpRequestMessage(HttpMethod.Patch, $"rentals/{rentalId}/status")
        {
            Content = JsonContent.Create(body, options: ApiJsonOptions.Default),
        };
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var wire = await response.Content.ReadFromJsonAsync<UpdateStatusResponseWire>(ApiJsonOptions.Default, ct)
                   ?? throw new InvalidOperationException("Empty response body from PATCH /rentals/{id}/status.");
        return new RentalStatusUpdate(wire.Id, ParseStatus(wire.Status), wire.UpdatedAt);
    }

    private async Task<IReadOnlyList<Rental>> GetRentalsAsync(string baseUrl, RentalQuery? query, CancellationToken ct)
    {
        var url = baseUrl;
        if (query?.Status is RentalStatus s)
        {
            url += $"?status={s}";
        }
        var wire = await _http.GetFromJsonAsync<RentalsEnvelopeWire>(url, ApiJsonOptions.Default, ct);
        return (wire?.Rentals ?? Array.Empty<RentalSummaryWire>()).Select(ToModel).ToList();
    }

    // ---- Wire records -----------------------------------------------------

    /// <summary>
    /// Common shape used for POST <c>/rentals</c> response and incoming/outgoing
    /// list items. Uses <c>createdAt</c>. <c>itemDescription</c> is absent here
    /// (only on detail GET).
    /// </summary>
    private sealed record RentalSummaryWire(
        int Id,
        int ItemId,
        string? ItemTitle,
        decimal? ItemDailyRate,
        int BorrowerId,
        string? BorrowerName,
        int? OwnerId,
        string? OwnerName,
        string StartDate,
        string EndDate,
        string Status,
        decimal TotalPrice,
        DateTime? CreatedAt);

    /// <summary>
    /// Shape returned by <c>GET /rentals/{id}</c>. Uses <c>requestedAt</c>
    /// rather than <c>createdAt</c>, and includes <c>itemDescription</c>.
    /// </summary>
    private sealed record RentalDetailWire(
        int Id,
        int ItemId,
        string? ItemTitle,
        string? ItemDescription,
        int BorrowerId,
        string? BorrowerName,
        int? OwnerId,
        string? OwnerName,
        string StartDate,
        string EndDate,
        string Status,
        decimal TotalPrice,
        DateTime RequestedAt);

    /// <summary>
    /// Envelope for GET <c>/rentals/incoming</c> and <c>/rentals/outgoing</c>:
    /// <c>{ rentals, totalRentals }</c>.
    /// </summary>
    private sealed record RentalsEnvelopeWire(
        IReadOnlyList<RentalSummaryWire>? Rentals,
        int? TotalRentals);

    private sealed record UpdateStatusResponseWire(
        int Id,
        string Status,
        DateTime UpdatedAt);

    // Outgoing bodies. Dates are sent as strings to match the spec's regex.
    private sealed record CreateRentalBody(int ItemId, string StartDate, string EndDate);

    private sealed record UpdateStatusBody(string Status);

    // ---- Mappers ----------------------------------------------------------

    private static Rental ToModel(RentalSummaryWire w) => new()
    {
        Id = w.Id,
        ItemId = w.ItemId,
        BorrowerId = w.BorrowerId,
        StartDate = ParseDate(w.StartDate),
        EndDate = ParseDate(w.EndDate),
        Status = ParseStatus(w.Status),
        TotalPrice = w.TotalPrice,
        CreatedAt = w.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = w.CreatedAt ?? DateTime.UtcNow,

        ItemTitle = w.ItemTitle ?? string.Empty,
        ItemDailyRate = w.ItemDailyRate,
        BorrowerName = w.BorrowerName ?? string.Empty,
        OwnerName = w.OwnerName ?? string.Empty,
    };

    private static Rental ToModel(RentalDetailWire w) => new()
    {
        Id = w.Id,
        ItemId = w.ItemId,
        BorrowerId = w.BorrowerId,
        StartDate = ParseDate(w.StartDate),
        EndDate = ParseDate(w.EndDate),
        Status = ParseStatus(w.Status),
        TotalPrice = w.TotalPrice,
        CreatedAt = w.RequestedAt,
        UpdatedAt = w.RequestedAt,

        ItemTitle = w.ItemTitle ?? string.Empty,
        ItemDailyRate = null,
        BorrowerName = w.BorrowerName ?? string.Empty,
        OwnerName = w.OwnerName ?? string.Empty,
    };

    private static DateOnly ParseDate(string s) =>
        DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Defensive enum parse. The API types <c>status</c> as bare string with no
    /// enum constraint, and the requirements wording ("Out for Rent",
    /// "Returned") doesn't perfectly match our <see cref="RentalStatus"/> enum
    /// names. If a wire value can't be matched, throw a clear error so Phase 5
    /// work can pin the canonical strings rather than silently mapping to
    /// Pending and hiding the bug.
    /// </summary>
    private static RentalStatus ParseStatus(string s)
    {
        if (Enum.TryParse<RentalStatus>(s, ignoreCase: true, out var v) && Enum.IsDefined(v))
        {
            return v;
        }
        throw new InvalidDataException(
            $"Unknown rental status from API: '{s}'. Update RentalStatus enum or the wire-mapping table.");
    }
}
