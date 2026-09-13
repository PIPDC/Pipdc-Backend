using PIPDC.Domain.Common;

namespace PIPDC.Application.AiChat;

public sealed record GeminiMessage(string Role, string Content);

public sealed record GeminiToolCall(string Name, string JsonArguments);

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
    public int? Bedrooms { get; set; }
    public string? ListingType { get; set; }
}