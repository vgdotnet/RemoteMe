using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteMe.Core.Security;

// Reads the secret straight from the UTF-8 JSON buffer, so no plain-text string is created.
internal sealed class ProtectedSecretJsonConverter : JsonConverter<ProtectedSecret> {
    public override ProtectedSecret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.String) {
            throw new JsonException("A secret must be a JSON string.");
        }
        if (!reader.ValueIsEscaped && !reader.HasValueSequence) {
            return ProtectedSecret.FromUtf8(reader.ValueSpan);
        }
        var length = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length;
        var buffer = GC.AllocateArray<byte>(length, pinned: true);
        try {
            var written = reader.CopyString(buffer);
            return ProtectedSecret.FromUtf8(buffer.AsSpan(0, written));
        } finally {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProtectedSecret value, JsonSerializerOptions options) {
        value.UseUtf8(utf8 => writer.WriteStringValue(utf8));
    }
}
