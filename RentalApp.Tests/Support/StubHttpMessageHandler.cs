namespace RentalApp.Tests.Support;

/// <summary>
/// Minimal HttpMessageHandler fake. Accepts a delegate that decides how to
/// respond to each intercepted request, and records every request for later
/// assertions. Use when testing DelegatingHandlers, HttpClient-backed
/// services, or repositories without standing up a real HTTP server.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public StubHttpMessageHandler(HttpResponseMessage response)
        : this(_ => response)
    {
    }

    /// <summary>Responds to successive calls by dequeuing the next response.</summary>
    public static StubHttpMessageHandler Sequence(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);
        return new StubHttpMessageHandler(_ => queue.Dequeue());
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_handler(request));
    }
}
