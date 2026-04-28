using System.Diagnostics;

namespace RentalApp.Services;

/// <summary>
/// MAUI-backed implementation of <see cref="ILocationService"/>. Uses
/// <see cref="Geolocation.Default"/> for the GPS read and the
/// <see cref="Permissions.LocationWhenInUse"/> API for the runtime prompt.
/// MAUI-only — kept out of the test project's compile unit.
/// </summary>
public sealed class LocationService : ILocationService
{
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
            // gracefully fall back to manual entry. We deliberately don't
            // re-throw because losing the GPS read shouldn't crash the page.
            Debug.WriteLine($"LocationService.GetCurrentLocationAsync failed: {ex}");
            return null;
        }
    }
}
