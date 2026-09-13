using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PIPDC.Application.Data;
using PIPDC.Application.Properties;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.AiChat;

public class AiChatService(
    IAppDbContext dbContext,
    IGeminiClient geminiClient,
    IPropertyService propertyService,
    ILogger<AiChatService> logger) : IAiChatService
{
    private const string SystemPrompt =
        "You are the PIPDC property assistant. You help people find real properties listed on the PIPDC marketplace in Nigeria. " +
        "Rules you must always follow:" +
        " 1. NEVER invent, guess, or fabricate property listings. You may only recommend properties returned by the search_properties tool, which queries the live PIPDC database." +
        " 2. Only call search_properties after the user has given a LOCATION (city, state, or neighbourhood) AND at least a budget (min/max price) or a neighbourhood AREA. If any of location, budget, or area is missing, ask ONE short clarifying question instead of searching." +
        " 3. When search_properties returns results, recommend at most three of the returned listings and briefly highlight one or two relevant features of each. Never describe listings that the tool did not return." +
        " 4. If the tool reports no matches, suggest how the user could broaden the search (different location, wider budget, or a nearby area)." +
        " 5. Be concise and friendly. Prices keep the currency the marketplace uses.";

    private const string ClarificationHint =
        "The user has not given enough detail to run a property search. Do NOT call the tool again. Ask ONE short clarifying question requesting: the preferred location, and either a budget range or a specific neighbourhood/area.";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<Result<AiChatSessionDto>> GetSessionAsync(string userId, CancellationToken ct)
    {
        var session = await LoadSessionAsync(userId, ct);
        if (session is null)
            return Result<AiChatSessionDto>.Failure(
                Error.NotFound("aichat.nosession", "No AI assistant session exists yet."));

        var persisted = DeserializeMessages(session.MessagesJson);
        return Result<AiChatSessionDto>.Success(ToSessionDto(session, persisted));
    }

    public async Task<Result<SendAiMessageResponseDto>> SendAsync(string userId, string content, CancellationToken ct)
    {
        var session = await LoadSessionAsync(userId, ct);
        var persisted = session is null ? new List<PersistedAiMessage>() : DeserializeMessages(session.MessagesJson);

        var history = persisted
            .TakeLast(11)
            .Select(m => new GeminiMessage(m.Role, m.Content))
            .Append(new GeminiMessage("user", content))
            .ToList();

        GeminiTurnResult turn;
        try
        {
            var turnResult = await geminiClient.TurnAsync(history, SystemPrompt, ct);
            if (turnResult.IsFailure)
                return Result<SendAiMessageResponseDto>.Failure(turnResult.Error);
            turn = turnResult.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI assistant first turn failed for user {UserId}.", userId);
            return Result<SendAiMessageResponseDto>.Failure(AiUnavailable());
        }

        var assistantText = turn.Text ?? string.Empty;
        List<PropertyDto>? recommendations = null;

        if (turn.ToolCall is not null)
        {
            var args = ParseToolArgs(turn.ToolCall);
            if (args is null)
                return Result<SendAiMessageResponseDto>.Failure(
                    Error.Validation("aichat.badtoolargs", "The assistant produced an invalid search request. Please try rephrasing."));

            var missingDetails = string.IsNullOrWhiteSpace(args.Location)
                || (string.IsNullOrWhiteSpace(args.Area) && !args.MinPrice.HasValue && !args.MaxPrice.HasValue);

            var toolResultText = ClarificationHint;
            List<PropertyDto> found;
            if (!missingDetails)
            {
                found = await SearchPropertiesAsync(args, ct);
                toolResultText = BuildToolResultText(found);
                recommendations = found;
            }

            string finalText;
            try
            {
                var finalResult = await geminiClient.CompleteAfterToolAsync(history, SystemPrompt, turn.ToolCall, toolResultText, ct);
                if (finalResult.IsFailure)
                    return Result<SendAiMessageResponseDto>.Failure(finalResult.Error);
                finalText = finalResult.Value;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI assistant tool-follow turn failed for user {UserId}.", userId);
                return Result<SendAiMessageResponseDto>.Failure(AiUnavailable());
            }

            assistantText = finalText;
        }

        if (session is null)
        {
            session = new AiChatSession
            {
                UserId = userId,
                Title = content.Length > 60 ? content[..60] : content,
                MessagesJson = "[]",
                LastMessageAt = DateTime.UtcNow
            };
            dbContext.AiChatSessions.Add(session);
        }

        persisted.Add(new PersistedAiMessage { Role = "user", Content = content, SentAt = DateTime.UtcNow });
        persisted.Add(new PersistedAiMessage
        {
            Role = "model",
            Content = assistantText,
            SentAt = DateTime.UtcNow,
            Properties = recommendations
        });

        session.MessagesJson = JsonSerializer.Serialize(persisted, JsonOpts);
        session.LastMessageAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var assistantDto = ToMessageDto(persisted[^1]);
        return Result<SendAiMessageResponseDto>.Success(
            new SendAiMessageResponseDto(ToSessionDto(session, persisted), assistantDto));
    }

    public async Task<Result> DeleteAsync(string userId, CancellationToken ct)
    {
        var session = await LoadSessionAsync(userId, ct);
        if (session is null)
            return Result.Failure(
                Error.NotFound("aichat.nosession", "No AI assistant session exists yet."));

        dbContext.AiChatSessions.Remove(session);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private Task<AiChatSession?> LoadSessionAsync(string userId, CancellationToken ct) =>
        dbContext.AiChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastMessageAt)
            .FirstOrDefaultAsync(ct);

    private static List<PersistedAiMessage> DeserializeMessages(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<PersistedAiMessage>();

        try
        {
            return JsonSerializer.Deserialize<List<PersistedAiMessage>>(json, JsonOpts) ?? new();
        }
        catch (JsonException)
        {
            return new List<PersistedAiMessage>();
        }
    }

    private static GeminiToolArgs? ParseToolArgs(GeminiToolCall call)
    {
        if (!string.Equals(call.Name, "search_properties", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.IsNullOrWhiteSpace(call.JsonArguments))
            return new GeminiToolArgs();

        try
        {
            return JsonSerializer.Deserialize<GeminiToolArgs>(call.JsonArguments, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<List<PropertyDto>> SearchPropertiesAsync(GeminiToolArgs args, CancellationToken ct)
    {
        var query = new PropertyQueryParameters
        {
            Location = string.IsNullOrWhiteSpace(args.Area) ? args.Location : args.Area,
            Query = args.Location,
            MinPrice = args.MinPrice,
            MaxPrice = args.MaxPrice,
            Bedrooms = args.Bedrooms,
            ListingType = args.ListingType,
            Status = "Available",
            PageNumber = 1,
            PageSize = 3
        };

        var result = await propertyService.GetAllAsync(query, null, ct);
        return result.IsSuccess ? result.Value.Items : new List<PropertyDto>();
    }

    private static string BuildToolResultText(IReadOnlyList<PropertyDto> properties)
    {
        if (properties.Count == 0)
            return "No properties in the PIPDC database match the current search. Tell the user no listing currently matches, and suggest they relax the location, budget, or area.";

        var lines = properties.Select((p, i) =>
            $"{i + 1}. \"{p.Title}\" (slug: {p.Slug}) — {p.Currency} {p.Price:N0} ({p.ListingType}), {p.City}, {p.State}" +
            (p.Area is { Length: > 0 } ? $", {p.Area}" : string.Empty) +
            (p.Bedrooms.HasValue ? $", {p.Bedrooms} bedroom{(p.Bedrooms == 1 ? string.Empty : "s")}" : string.Empty));

        return "The property search tool found " + properties.Count +
               (properties.Count == 1 ? " match:" : " matches:") + Environment.NewLine +
               string.Join(Environment.NewLine, lines);
    }

    private static Error AiUnavailable() =>
        Error.Failure("aichat.unavailable", "The AI assistant is temporarily unavailable. Please try again shortly.");

    private static AiChatSessionDto ToSessionDto(AiChatSession session, List<PersistedAiMessage> messages) =>
        new(session.Id, session.Title, session.LastMessageAt, messages.Select(ToMessageDto).ToList());

    private static AiChatMessageDto ToMessageDto(PersistedAiMessage message) =>
        new(message.Role, message.Content, message.SentAt, message.Properties);
}