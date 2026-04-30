namespace RentalApp.Services;

/// <summary>
/// A JWT together with the metadata needed to decide whether it is still usable.
/// Persisted by <see cref="ITokenStorage"/> and attached to outbound requests by
/// <see cref="AuthDelegatingHandler"/>.
/// </summary>
/// <param name="Token">Raw JWT string returned by POST /auth/token.</param>
/// <param name="ExpiresAt">UTC expiry returned by the API alongside the token.</param>
/// <param name="UserId">The id of the authenticated user (for rehydration).</param>
public record StoredToken(string Token, DateTime ExpiresAt, int UserId)
{
    /// <summary>
    /// Default skew applied before expiry so we don't send a request that
    /// is about to become invalid server-side between our check and the
    /// server's validation.
    /// </summary>
    public static readonly TimeSpan DefaultExpirySkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// True if the token is expired (or close enough to expiry that it isn't
    /// worth using). <paramref name="now"/> and <paramref name="skew"/> are
    /// overridable for deterministic unit testing.
    /// </summary>
    public bool IsExpired(DateTime? now = null, TimeSpan? skew = null)
        => ExpiresAt <= (now ?? DateTime.UtcNow).Add(skew ?? DefaultExpirySkew);
}
