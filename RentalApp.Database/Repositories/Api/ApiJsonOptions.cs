using System.Text.Json;
using System.Text.Json.Serialization;

namespace RentalApp.Database.Repositories.Api;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for the API repositories.
/// The hosted API speaks camelCase JSON; serialise/deserialise with the
/// same settings everywhere so wire records can stay PascalCase like the
/// rest of the C# code. <see cref="JsonStringEnumConverter"/> is registered
/// so <see cref="Models.RentalStatus"/> round-trips as a string.
/// </summary>
internal static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Default;

    static ApiJsonOptions()
    {
        Default = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        Default.Converters.Add(new JsonStringEnumConverter());
    }
}
