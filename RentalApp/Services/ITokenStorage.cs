namespace RentalApp.Services;

/// <summary>
/// Persists the authenticated user's JWT across app sessions.
///
/// Implementations:
///   * <see cref="SecureStorageTokenStorage"/> — production, backed by MAUI SecureStorage.
///   * In-memory test double lives in RentalApp.Tests.
///
/// Callers should usually go through <see cref="TokenStorageExtensions.GetValidTokenAsync"/>
/// rather than calling <see cref="LoadAsync"/> directly — it applies the proactive
/// expiry check defined on <see cref="StoredToken"/>.
/// </summary>
public interface ITokenStorage
{
    Task<StoredToken?> LoadAsync();
    Task SaveAsync(StoredToken token);
    Task ClearAsync();
}
