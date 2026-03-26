using System.Text.Encodings.Web;
using System.Text.Json;

namespace RoslynLens;

internal static class Json
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);
}
