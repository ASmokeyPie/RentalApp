using System.Diagnostics;
using RentalApp.Database.Repositories;

namespace RentalApp.Services;

/// <summary>
/// MAUI-backed implementation of <see cref="ILocationService"/>. Uses
/// <see cref="Geolocation.Default"/> for the GPS read,
/// <see cref="Permissions.LocationWhenInUse"/> for the runtime prompt, and
/// <see cref="IItemRepository.GetNearbyAsync"/> for the radius query — the
/// PostGIS work itself happens server-side. MAUI-only — kept out of the
/// test project's compile unit.
/// </summary>
public sealed class LocationService : ILocationService
{
    private readonly IItemRepository _items;

    public LocationService(IItemRepository items) => _items = items;

    public async Task<(double Latitude, double Longitude)?> GetCurrentLocationAsync(CancellationToken ct = default)
    {
        try
        {
            // Ensure we have permission before asking for a fix.
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    return null;
                }
            }

            // Try a cached location first — instant, but may be stale.
            var cached = await Geolocation.Default.GetLastKnownLocationAsync();
            if (cached is not null)
            {
                return (cached.Latitude, cached.Longitude);
            }

            // Fall back to a fresh fix. Medium accuracy is plenty for "near me"
            // at radius ≥ a few hundred metres and is much faster than Best.
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request, ct);
            if (location is null) return null;

            return (location.Latitude, location.Longitude);
        }
        catch (FeatureNotSupportedException)
        {
            // Device doesn't support geolocation (e.g. some emulators).
            return null;
        }
        catch (FeatureNotEnabledException)
        {
            // GPS turned off in OS settings.
            return null;
        }
        catch (PermissionException)
        {
            return null;
        }
        catch (Exception ex)
        {
            // Anything else — log and treat as "no fix" so the caller can
            // gracefully fall back to manual entry. Deliberately don't
            // re-throw because losing the GPS read shouldn't crash the page.
            Debug.WriteLine($"LocationService.GetCurrentLocationAsync failed: {ex}");
            return null;
        }
    }

    public async Task<NearbySearchResult?> FindNearbyItemsAsync(
        double radiusKm,
        string? categorySlug = null,
        CancellationToken ct = default)
    {
        var location = await GetCurrentLocationAsync(ct);
        if (location is null) return null;

        var (lat, lon) = location.Value;
        var items = await _items.GetNearbyAsync(lat, lon, radiusKm, categorySlug, ct);
        return new NearbySearchResult(lat, lon, items);
    }
}
