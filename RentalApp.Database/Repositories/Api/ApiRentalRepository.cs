using System.Globalization;
using System.Net.Http.Json;
using RentalApp.Database.Helpers;
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
/// Date strings come back in either <c>"yyyy-MM-dd"</c> (per spec) or full
/// ISO 8601 (<c>"2026-04-27T00:00:00.000Z"</c>, what the live API actually
/// emits). <see cref="ParseDate"/> handles both.
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
        await response.EnsureSuccessOrThrowApiErrorAsync(ct);
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
        await response.EnsureSuccessOrThrowApiErrorAsync(ct);
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
        var body = new UpdateStatusBody(ToWireString(newStatus));
        var request = new HttpRequestMessage(HttpMethod.Patch, $"rentals/{rentalId}/status")
        {
            Content = JsonContent.Create(body, options: ApiJsonOptions.Default),
        };
        var response = await _http.SendAsync(request, ct);
        await response.EnsureSuccessOrThrowApiErrorAsync(ct);
        var wire = await response.Content.ReadFromJsonAsync<UpdateStatusResponseWire>(ApiJsonOptions.Default, ct)
                   ?? throw new InvalidOperationException("Empty response body from PATCH /rentals/{id}/status.");
        return new RentalStatusUpdate(wire.Id, ParseStatus(wire.Status), wire.UpdatedAt);
    }

    private async Task<IReadOnlyList<Rental>> GetRentalsAsync(string baseUrl, RentalQuery? query, CancellationToken ct)
    {
        var url = baseUrl;
        if (query?.Status is RentalStatus s)
        {
            url += $"?status={Uri.EscapeDataString(ToWireString(s))}";
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

    private static Rental ToModel(RentalSummaryWire w)
    {
        var endDate = ParseDate(w.EndDate);
        return new()
        {
            Id = w.Id,
            ItemId = w.ItemId,
            BorrowerId = w.BorrowerId,
            StartDate = ParseDate(w.StartDate),
            EndDate = endDate,
            Status = RentalStatusHelper.DeriveStatus(ParseStatus(w.Status), endDate),
            TotalPrice = w.TotalPrice,
            CreatedAt = w.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = w.CreatedAt ?? DateTime.UtcNow,

            ItemTitle = w.ItemTitle ?? string.Empty,
            ItemDailyRate = w.ItemDailyRate,
            BorrowerName = w.BorrowerName ?? string.Empty,
            OwnerId = w.OwnerId ?? 0,
            OwnerName = w.OwnerName ?? string.Empty,
        };
    }

    private static Rental ToModel(RentalDetailWire w)
    {
        var endDate = ParseDate(w.EndDate);
        return new()
        {
            Id = w.Id,
            ItemId = w.ItemId,
            BorrowerId = w.BorrowerId,
            StartDate = ParseDate(w.StartDate),
            EndDate = endDate,
            Status = RentalStatusHelper.DeriveStatus(ParseStatus(w.Status), endDate),
            TotalPrice = w.TotalPrice,
            CreatedAt = w.RequestedAt,
            UpdatedAt = w.RequestedAt,

            ItemTitle = w.ItemTitle ?? string.Empty,
            ItemDailyRate = null,
            BorrowerName = w.BorrowerName ?? string.Empty,
            OwnerId = w.OwnerId ?? 0,
            OwnerName = w.OwnerName ?? string.Empty,
        };
    }

    /// <summary>
    /// Date parser tolerant of the two formats the API has been observed
    /// returning: the spec-stated <c>"yyyy-MM-dd"</c> and full ISO 8601
    /// datetimes (e.g. <c>"2026-04-27T00:00:00.000Z"</c>) for items posted by
    /// other clients. Falls back to a length-10 prefix slice so even unusual
    /// ISO variants (e.g. mis-punctuated millisecond separators) still yield
    /// the date.
    /// </summary>
    private static DateOnly ParseDate(string s)
    {
        if (DateOnly.TryParseExact(
                s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        if (DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            return DateOnly.FromDateTime(dto.UtcDateTime);
        }

        // Last-resort: any ISO-shaped string starts with "yyyy-MM-dd".
        if (s.Length >= 10
            && DateOnly.TryParseExact(
                s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var prefix))
        {
            return prefix;
        }

        throw new InvalidDataException($"Could not parse '{s}' as a rental date.");
    }

    // ---- Status enum ↔ wire-string translation ---------------------------
    //
    // The hosted API uses the requirements-doc wording for rental statuses,
    // which means "Out for Rent" with spaces — not "OutForRent". Most other
    // status names happen to be a single word and round-trip identically, but
    // the moment we ignore the translation we get 409s like "Cannot transition
    // from Approved to OutForRent" because the server doesn't recognise the
    // CamelCased form.

    /// <summary>
    /// Returns the wire string the server expects for a given enum value.
    /// </summary>
    private static string ToWireString(RentalStatus s) => s switch
    {
        RentalStatus.Requested  => "Requested",
        RentalStatus.Approved   => "Approved",
        RentalStatus.Rejected   => "Rejected",
        RentalStatus.OutForRent => "Out for Rent",
        // Overdue is client-side only; the server still sees it as Out for Rent.
        // We never PATCH with Overdue as the target — but map it defensively.
        RentalStatus.Overdue    => "Out for Rent",
        RentalStatus.Returned   => "Returned",
        RentalStatus.Completed  => "Completed",
        _ => s.ToString(),
    };

    /// <summary>
    /// Wire-string → enum, tolerant of casing and a couple of plausible
    /// variants (CamelCase, Title Case, snake_case) so the parser doesn't
    /// throw if a sister client sends a slightly different form.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, RentalStatus> FromWire =
        new Dictionary<string, RentalStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["Requested"]    = RentalStatus.Requested,
            ["Approved"]     = RentalStatus.Approved,
            ["Rejected"]     = RentalStatus.Rejected,
            ["Out for Rent"] = RentalStatus.OutForRent,
            ["Out For Rent"] = RentalStatus.OutForRent,
            ["OutForRent"]   = RentalStatus.OutForRent,
            ["out_for_rent"] = RentalStatus.OutForRent,
            ["Returned"]     = RentalStatus.Returned,
            ["Completed"]    = RentalStatus.Completed,
        };

    /// <summary>
    /// Defensive enum parse. Honours <see cref="FromWire"/> first (so spaced
    /// forms like "Out for Rent" map correctly) and falls back to a strict
    /// enum-name parse for robustness. Throws on anything we don't recognise
    /// so wire-vs-enum drift surfaces loudly rather than silently mapping to
    /// a default.
    /// </summary>
    private static RentalStatus ParseStatus(string s)
    {
        if (FromWire.TryGetValue(s, out var fromTable))
        {
            return fromTable;
        }
        if (Enum.TryParse<RentalStatus>(s, ignoreCase: true, out var v) && Enum.IsDefined(v))
        {
            return v;
        }
        throw new InvalidDataException(
            $"Unknown rental status from API: '{s}'. Update RentalStatus enum or the wire-mapping table.");
    }
}
