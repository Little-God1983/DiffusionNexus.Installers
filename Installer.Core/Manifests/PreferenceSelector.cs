using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIKnowledge2Go.Installers.Core.Manifests;

public sealed record PreferenceSelector
{
    public IReadOnlyList<string>? Values { get; init; }

    public string? Reference { get; init; }

    public override string ToString() => Reference ?? (Values is null ? string.Empty : string.Join(", ", Values));
}

public sealed class PreferenceSelectorJsonConverter : JsonConverter<PreferenceSelector>
{
    public override PreferenceSelector? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var values = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("Preference arrays must contain string values.");
                }

                values.Add(reader.GetString()!);
            }

            return new PreferenceSelector
            {
                Values = values,
            };
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return new PreferenceSelector
            {
                Reference = reader.GetString(),
            };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var reference = root.TryGetProperty("reference", out var referenceProperty)
                ? referenceProperty.GetString()
                : null;
            var values = root.TryGetProperty("values", out var valuesProperty)
                ? valuesProperty.EnumerateArray().Select(v => v.GetString()!).ToArray()
                : null;

            return new PreferenceSelector
            {
                Reference = reference,
                Values = values,
            };
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        throw new JsonException($"Unsupported token {reader.TokenType} while parsing preference selector.");
    }

    public override void Write(Utf8JsonWriter writer, PreferenceSelector value, JsonSerializerOptions options)
    {
        if (value.Reference is not null)
        {
            writer.WriteStringValue(value.Reference);
            return;
        }

        if (value.Values is not null)
        {
            writer.WriteStartArray();
            foreach (var item in value.Values)
            {
                writer.WriteStringValue(item);
            }

            writer.WriteEndArray();
            return;
        }

        writer.WriteNullValue();
    }
}
