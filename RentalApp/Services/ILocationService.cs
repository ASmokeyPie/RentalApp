namespace RentalApp.Services;

/// <summary>
/// Wraps the platform geolocation API behind a MAUI-free contract so
/// ViewModels can request the device's current location without depending on
/// <c>Microsoft.Maui.Devices.Sensors</c>. The implementation
/// (<see cref="LocationService"/>) lives in the MAUI head; this interface
/// stays in the test-compileable surface.
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
}
