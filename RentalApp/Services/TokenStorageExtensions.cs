namespace RentalApp.Services;

/// <summary>
/// Convenience helpers on top of <see cref="ITokenStorage"/>.
/// Centralised here so the proactive-expiry rule lives in exactly one place.
/// </summary>
public static class TokenStorageExtensions
{
    /// <summary>
    /// Returns the stored token only if it exists and is not expired (per
    /// <see cref="StoredToken.IsExpired"/>). Otherwise returns null.
    /// Does NOT clear expired tokens from storage — that is the caller's
    /// responsibility if desired (the 401 path in AuthDelegatingHandler
    /// already clears on server-side rejection).
    /// </summary>
    public static async Task<StoredToken?> GetValidTokenAsync(this ITokenStorage storage)
    {
        var token = await storage.LoadAsync();
        return token is not null && !token.IsExpired() ? token : null;
    }
}
