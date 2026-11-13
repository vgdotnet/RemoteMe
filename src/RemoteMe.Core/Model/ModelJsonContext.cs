using System.Text.Json.Serialization;

namespace RemoteMe.Core.Model;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(SchemaProbe))]
internal sealed partial class ModelJsonContext : JsonSerializerContext;
