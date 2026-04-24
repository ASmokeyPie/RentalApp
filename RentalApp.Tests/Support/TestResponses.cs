using System.Net;
using System.Text;
using System.Text.Json;

namespace RentalApp.Tests.Support;

/// <summary>
/// Helpers for building <see cref="HttpResponseMessage"/> instances with JSON
/// bodies. Property names follow the camelCase shapes the hosted API returns,
/// which System.Text.Json deserialises case-insensitively into our PascalCase
/// DTOs by default.
/// </summary>
internal static class TestResponses
{
    public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(body);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage Status(HttpStatusCode status) =>
        new(status);
}
