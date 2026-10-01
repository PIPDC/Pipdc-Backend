using System.Text.Json;
using System.Text.Json.Serialization;
using PIPDC.Domain.Common;

namespace PIPDC.Application.AiChat;

public sealed record GeminiMessage(string Role, string Content);

public sealed record GeminiToolCall(string Name, string JsonArguments, string? Id = null);

public sealed record GeminiTurnResult(string? Text, GeminiToolCall? ToolCall);

public interface IGeminiClient
{
    Task<Result<GeminiTurnResult>> TurnAsync(
        IReadOnlyList<GeminiMessage> history,
        string systemPrompt,
        CancellationToken ct);

    Task<Result<string>> CompleteAfterToolAsync(
        IReadOnlyList<GeminiMessage> history,
        string systemPrompt,
        GeminiToolCall toolCall,
        string toolResultText,
        CancellationToken ct);
}

public sealed class GeminiToolArgs
{
    public string? Location { get; set; }
    public string? Area { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    [JsonConverter(typeof(BedroomListJsonConverter))]
    public List<int>? Bedrooms { get; set; }
    public int? MinBedrooms { get; set; }
    public string? ListingType { get; set; }
}

public sealed class GeminiDevelopmentToolArgs
{
    public string? Location { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Accepts a single integer (e.g. {"bedrooms":3}) or an array of integers
/// (e.g. {"bedrooms":[2,6]}) for the exact-bedroom tool argument.
/// </summary>
public sealed class BedroomListJsonConverter : JsonConverter<List<int>?>
{
    public override List<int>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return new List<int> { reader.GetInt32() };

        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonSerializer.Deserialize<List<int>>(ref reader, options);

        throw new JsonException("bedrooms must be an integer or an array of integers.");
    }

    public override void Write(Utf8JsonWriter writer, List<int>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.Count == 1)
        {
            writer.WriteNumberValue(value[0]);
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteNumberValue(item);
        writer.WriteEndArray();
    }
}