using System.Net.Http.Json;

namespace RentalApp.Database.Repositories.Api;

/// <summary>
/// Helpers for translating non-2xx API responses into <see cref="HttpRequestException"/>s
/// that carry the server's user-facing <c>message</c> rather than the
/// default "Response status code does not indicate success: NNN ..." string.
/// </summary>
/// <remarks>
/// The hosted API consistently returns <c>{ "error": "...", "message": "..." }</c>
/// for 4xx/5xx responses (per the OpenAPI spec). When something fails the
/// VM's error banner can show "Item is already booked for these dates"
/// instead of the unhelpful default. Repository write methods that care
/// about precise error feedback (rental status transitions, rental requests,
/// item updates, etc.) should call <see cref="EnsureSuccessOrThrowApiErrorAsync"/>
/// in place of <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.
/// </remarks>
internal static class ApiErrorHandling
{
    /// <summary>
    /// Equivalent to <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
    /// but, on non-2xx, attempts to deserialise the spec's
    /// <c>{ error, message }</c> body and throws an
    /// <see cref="HttpRequestException"/> whose <see cref="Exception.Message"/>
    /// is the server-supplied <c>message</c>. Falls back to a generic
    /// status-line message if the body isn't readable.
    /// </summary>
    public static async Task EnsureSuccessOrThrowApiErrorAsync(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode) return;

        string? message = null;
        try
        {
            var body = await response.Content
                .ReadFromJsonAsync<ApiErrorBody>(ApiJsonOptions.Default, ct);
            message = body?.Message;
        }
        catch
        {
            // Body wasn't JSON, was empty, or didn't match the expected shape.
            // Fall through to the generic message below.
        }

        var fallback = $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}.";
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(message) ? fallback : message,
            inner: null,
            statusCode: response.StatusCode);
    }

    /// <summary>Spec error envelope.</summary>
    private sealed record ApiErrorBody(string? Error, string? Message);
}
