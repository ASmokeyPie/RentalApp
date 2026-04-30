using RentalApp.Database.Models;

namespace RentalApp.Services;

/// <summary>
/// Wraps the platform geolocation API behind a MAUI-free contract AND owns
/// the client-side spatial-discovery workflow ("find items near me").
/// The implementation (<see cref="LocationService"/>) lives in the MAUI head;
/// this interface stays in the test-compileable surface.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Returns the device's current latitude/longitude, or <c>null</c> if
    /// permission is denied, the platform refuses, or no fix is obtained
    /// within a reasonable timeout. Implementations should request the
    /// runtime permission as part of this call so callers don't need to.
    /// </summary>
    Task<(double Latitude, double Longitude)?> GetCurrentLocationAsync(CancellationToken ct = default);

    /// <summary>
    /// One-call "find items near me" workflow: reads the device's current
    /// coordinates and queries the items repository for everything within
    /// <paramref name="radiusKm"/> kilometres, optionally filtered by
    /// <paramref name="categorySlug"/>. Returns <c>null</c> if the GPS read
    /// failed (so the caller can render an actionable error). On success the
    /// result includes the search lat/lon for display purposes — ViewModels
    /// don't need to keep coordinates of their own.
    /// </summary>
    Task<NearbySearchResult?> FindNearbyItemsAsync(
        double radiusKm,
        string? categorySlug = null,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a successful <see cref="ILocationService.FindNearbyItemsAsync"/>.
/// Carries the search-origin coordinates so views can show "Showing items
/// near 55.95, -3.19" without recomputing them, plus the matched items
/// (each populated with its own lat/lon and <c>DistanceKm</c>).
/// </summary>
public sealed record NearbySearchResult(
    double Latitude,
    double Longitude,
    IReadOnlyList<Item> Items);
