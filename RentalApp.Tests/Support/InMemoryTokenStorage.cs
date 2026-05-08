using RentalApp.Services;

namespace RentalApp.Tests.Support;

/// <summary>
/// Deterministic in-memory replacement for <see cref="ITokenStorage"/> .
/// Also records call counts for assertions.
/// </summary>
public class InMemoryTokenStorage : ITokenStorage
{
    private StoredToken? _token;

    public int SaveCallCount { get; private set; }
    public int ClearCallCount { get; private set; }
    public int LoadCallCount { get; private set; }

    public InMemoryTokenStorage(StoredToken? initial = null)
    {
        _token = initial;
    }

    public Task<StoredToken?> LoadAsync()
    {
        LoadCallCount++;
        return Task.FromResult(_token);
    }

    public Task SaveAsync(StoredToken token)
    {
        _token = token;
        SaveCallCount++;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _token = null;
        ClearCallCount++;
        return Task.CompletedTask;
    }
}
