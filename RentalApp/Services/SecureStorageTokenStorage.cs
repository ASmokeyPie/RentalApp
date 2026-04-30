using System.Text.Json;
using Microsoft.Maui.Storage;

namespace RentalApp.Services;

/// <summary>
/// Production <see cref="ITokenStorage"/> backed by MAUI's platform SecureStorage
/// (Keychain on iOS/macCatalyst, EncryptedSharedPreferences on Android, DPAPI on
/// Windows). The token is JSON-serialised under a single key.
///
/// This class lives in the MAUI head project because it depends on
/// <see cref="SecureStorage"/>. Unit tests substitute a lightweight in-memory
/// implementation defined in RentalApp.Tests.
/// </summary>
public class SecureStorageTokenStorage : ITokenStorage
{
    private const string StorageKey = "rentalapp.auth.token";

    public async Task<StoredToken?> LoadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredToken>(json);
        }
        catch (JsonException)
        {
            // Corrupt value — drop to avoid getting stuck in a bad state.
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public Task SaveAsync(StoredToken token)
    {
        var json = JsonSerializer.Serialize(token);
        return SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
