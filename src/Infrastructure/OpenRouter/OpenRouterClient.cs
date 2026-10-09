using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PIPDC.Application.AiChat;
using PIPDC.Domain.Common;

namespace PIPDC.Infrastructure.OpenRouter;

/// <summary>
/// OpenAI-compatible chat-completions client that currently targets OpenRouter
/// (https://openrouter.ai/api/v1) so the conversational PIPDC property assistant can
/// be tested through a Gemini model served by OpenRouter. Implements the same
/// <see cref="IGeminiClient"/> abstraction the application layer already uses, so no
/// business logic changes are required: the live property search, the "never invent
/// properties" rule, and the tool-result round trip all stay in <c>AiChatService</c>.
/// </summary>
public sealed class OpenRouterClient(
    HttpClient http,
    IOptions<OpenRouterSettings> options,
    ILogger<OpenRouterClient> logger) : IGeminiClient
{
    private const string SearchToolName = "search_properties";
    private const string DevelopmentToolName = "search_developments";
    private const string EscalationToolName = "escalate_to_admin";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<Result<GeminiTurnResult>> TurnAsync(
        IReadOnlyList<GeminiMessage> history,
        string systemPrompt,
        CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Result<GeminiTurnResult>.Failure(
                Error.Failure("aichat.notconfigured", "The AI assistant is not configured on the server."));

        var request = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages = ToMessages(history, systemPrompt),
            Tools = new List<ChatTool> { SearchTool, DevelopmentTool, EscalationTool },
            ToolChoice = "auto",
            Temperature = 0.4,
            MaxTokens = 800
        };

        var parsed = await ExecuteAsync(request, settings, ct);
        if (parsed.IsFailure)
            return Result<GeminiTurnResult>.Failure(parsed.Error);

        var message = parsed.Value.Choices?[0].Message;
        var toolCall = message?.ToolCalls?.FirstOrDefault();
        if (toolCall is not null)
        {
            var id = string.IsNullOrWhiteSpace(toolCall.Id) ? $"call_{Guid.NewGuid():N}" : toolCall.Id;
            return Result<GeminiTurnResult>.Success(new GeminiTurnResult(
                null,
                new GeminiToolCall(toolCall.Function?.Name ?? SearchToolName, toolCall.Function?.Arguments ?? "{}", id)));
        }

        var text = message?.Content ?? string.Empty;
        return Result<GeminiTurnResult>.Success(
            new GeminiTurnResult(string.IsNullOrWhiteSpace(text) ? null : text, null));
    }

    public async Task<Result<string>> CompleteAfterToolAsync(
        IReadOnlyList<GeminiMessage> history,
        string systemPrompt,
        GeminiToolCall toolCall,
        string toolResultText,
        CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Result<string>.Failure(
                Error.Failure("aichat.notconfigured", "The AI assistant is not configured on the server."));

        var id = string.IsNullOrWhiteSpace(toolCall.Id)
            ? $"call_{Guid.NewGuid():N}"
            : toolCall.Id;

        var messages = ToMessages(history, systemPrompt);
        messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = null,
            ToolCalls = new List<ChatToolCall>
            {
                new()
                {
                    Id = id,
                    Type = "function",
                    Function = new ChatToolCallFunction
                    {
                        Name = toolCall.Name,
                        Arguments = string.IsNullOrWhiteSpace(toolCall.JsonArguments) ? "{}" : toolCall.JsonArguments
                    }
                }
            }
        });
        messages.Add(new ChatMessage
        {
            Role = "tool",
            ToolCallId = id,
            Content = toolResultText
        });

        var request = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages = messages,
            Temperature = 0.4,
            MaxTokens = 800
        };

        var parsed = await ExecuteAsync(request, settings, ct);
        if (parsed.IsFailure)
            return Result<string>.Failure(parsed.Error);

        var text = parsed.Value.Choices?[0].Message?.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return Result<string>.Failure(
                Error.Failure("aichat.upstream", "The AI assistant produced no reply. Please try again."));

        return Result<string>.Success(text);
    }

    private async Task<Result<ChatCompletionResponse>> ExecuteAsync(
        ChatCompletionRequest request,
        OpenRouterSettings settings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Combine explicitly so the configured base path (OpenRouter:BaseUrl, e.g.
        // https://openrouter.ai/api/v1) is always preserved and the request hits
        // .../api/v1/chat/completions. A relative URI against that base would drop
        // the "v1" segment (or the whole path with a leading slash).
        var endpoint = $"{settings.BaseUrl.TrimEnd('/')}/chat/completions";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Headers.Add("HTTP-Referer", "https://pipdc.plateaustate.gov.ng");
        httpRequest.Headers.Add("X-Title", "PIPDC Property Assistant");
        httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

        try
        {
            using var response = await http.SendAsync(httpRequest, ct);
            var status = (int)response.StatusCode;
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var category = ClassifyFailure(response.StatusCode);
                logger.LogWarning(
                    "OpenRouter {Provider} call failed for model {Model}: HTTP {Status} ({Category}) after {ElapsedMs}ms.",
                    settings.BaseUrl, settings.Model, status, category, sw.ElapsedMilliseconds);
                return Result<ChatCompletionResponse>.Failure(FailureOf(category, settings.Model));
            }

            ChatCompletionResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOpts, ct);
            }
            catch (JsonException)
            {
                sw.Stop();
                logger.LogWarning(
                    "OpenRouter {Provider} returned a non-JSON body for model {Model} after {ElapsedMs}ms.",
                    settings.BaseUrl, settings.Model, sw.ElapsedMilliseconds);
                return Result<ChatCompletionResponse>.Failure(
                    Error.Failure("aichat.malformed", "The AI assistant returned an unreadable response. Please try again."));
            }

            sw.Stop();
            logger.LogInformation(
                "OpenRouter {Provider} call succeeded for model {Model} in {ElapsedMs}ms.",
                settings.BaseUrl, settings.Model, sw.ElapsedMilliseconds);

            if (parsed is null || parsed.Choices is not { Count: > 0 } || parsed.Choices[0].Message is null)
                return Result<ChatCompletionResponse>.Failure(
                    Error.Failure("aichat.malformed", "The AI assistant returned an unreadable response. Please try again."));

            return Result<ChatCompletionResponse>.Success(parsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning("OpenRouter call timed out for model {Model} after {ElapsedMs}ms.",
                settings.Model, sw.ElapsedMilliseconds);
            return Result<ChatCompletionResponse>.Failure(
                Error.Failure("aichat.timeout", "The AI assistant took too long to respond. Please try again."));
        }
        catch (HttpRequestException)
        {
            sw.Stop();
            logger.LogWarning("OpenRouter request failed for model {Model} after {ElapsedMs}ms.",
                settings.Model, sw.ElapsedMilliseconds);
            return Result<ChatCompletionResponse>.Failure(
                Error.Failure("aichat.upstream", "The AI assistant is temporarily unavailable. Please try again shortly."));
        }
    }

    private static string ClassifyFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "invalidkey",
        HttpStatusCode.Forbidden => "forbidden",
        HttpStatusCode.NotFound => "modelunavailable",
        HttpStatusCode.TooManyRequests => "ratelimited",
        (HttpStatusCode)402 => "insufficientfunds",
        _ => "upstream"
    };

    private static Error FailureOf(string category, string model) => category switch
    {
        "invalidkey" => Error.Failure("aichat.invalidkey", "The AI assistant API key is invalid or missing. Please check the server configuration."),
        "forbidden" => Error.Failure("aichat.forbidden", "The AI assistant provider denied the request. Please check the server configuration."),
        "modelunavailable" => Error.Failure("aichat.modelunavailable", $"The AI assistant model ({model}) is not available. Please check the server configuration."),
        "ratelimited" => Error.Failure("aichat.ratelimited", "The AI assistant is receiving too many requests. Please try again shortly."),
        "insufficientfunds" => Error.Failure("aichat.insufficientfunds", "The AI assistant provider requires more credit. Please top up the OpenRouter account."),
        _ => Error.Failure("aichat.upstream", "The AI assistant is temporarily unavailable. Please try again shortly.")
    };

    private static List<ChatMessage> ToMessages(IReadOnlyList<GeminiMessage> history, string systemPrompt)
    {
        var messages = new List<ChatMessage> { new() { Role = "system", Content = systemPrompt } };
        foreach (var m in history)
        {
            // The persisted chat roles are "user" and "model"; OpenAI-compatible APIs
            // use "user" and "assistant".
            messages.Add(new ChatMessage
            {
                Role = string.Equals(m.Role, "model", StringComparison.OrdinalIgnoreCase) ? "assistant" : m.Role,
                Content = m.Content
            });
        }

        return messages;
    }

    private static readonly Dictionary<string, object> SearchFunctionParameters = new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["location"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "City, state, or neighbourhood the user is interested in (e.g. Jos, Lagos, Abuja)."
            },
            ["area"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Specific neighbourhood or area within the location, if the user mentioned one (e.g. Rayfield, Gwarinpa)."
            },
            ["minPrice"] = new Dictionary<string, object>
            {
                ["type"] = "number",
                ["description"] = "Minimum budget the user mentioned, if any."
            },
            ["maxPrice"] = new Dictionary<string, object>
            {
                ["type"] = "number",
                ["description"] = "Maximum budget the user mentioned, if any."
            },
            ["bedrooms"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object> { ["type"] = "integer" },
                ["description"] = "EXACT number(s) of bedrooms the user asked for. Array with ONE number for a specific count (e.g. [3] for '3-bedroom'), or SEVERAL numbers for multiple specific counts (e.g. [2, 6] for '2 or 6 bedrooms'). These are exact counts, NOT a minimum — never pass a lower floor to stand in for an exact count."
            },
            ["minBedrooms"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "A MINIMUM number of bedrooms, only when the user asks for 'at least N', 'N or more', or 'minimum N' bedrooms (e.g. 4 for 'at least 4 bedrooms'). Do NOT use for specific counts like '3-bedroom' — use bedrooms instead."
            },
            ["listingType"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = new[] { "ForSale", "ForLease" },
                ["description"] = "Whether the user wants to buy (ForSale) or rent (ForLease), if stated."
            }
        }
    };

    private static readonly ChatTool SearchTool = new()
    {
        Type = "function",
        Function = new ChatToolFunction
        {
            Name = SearchToolName,
            Description = "Searches the live PIPDC property database for listings matching the user's request and returns up to three matching properties. Call it as soon as the user provides ANY one concrete search detail — a location, an area or neighbourhood, a budget range, a number of bedrooms, or a listing type. For a neighbourhood like Rayfield passed as location/area, search immediately; do NOT ask for a city or state first. Filter strictly by the user's exact criteria: pass the EXACT bedroom count(s) requested, the stated location, budget and listing type; never search broader than the user asked for.",
            Parameters = SearchFunctionParameters
        }
    };

    private static readonly Dictionary<string, object> DevelopmentFunctionParameters = new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["location"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Optional city or area to narrow ongoing and upcoming development projects to (e.g. Jos, Rayfield, Bukuru)."
            },
            ["status"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = new[] { "Planned", "UnderConstruction", "NearCompletion" },
                ["description"] = "Optional status filter for development projects. Leave empty to return all ongoing and upcoming projects."
            }
        }
    };

    private static readonly ChatTool DevelopmentTool = new()
    {
        Type = "function",
        Function = new ChatToolFunction
        {
            Name = DevelopmentToolName,
            Description = "Looks up PIPDC's ongoing and upcoming property DEVELOPMENT projects — estates and developments still being planned, under construction or near completion — and returns up to six projects with name, status, progress percentage, location, expected completion, unit availability and the latest update. Call it when the user asks about ongoing, upcoming, future, in-progress or under-construction projects, development estates, or investment opportunities still being built. Do NOT use it for finished properties available now — those use search_properties.",
            Parameters = DevelopmentFunctionParameters
        }
    };

    private static readonly Dictionary<string, object> EscalationFunctionParameters = new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["reason"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Concise reason the chat is being handed to a PIPDC team member, describing what the user needs (aim for under ~200 characters)."
            }
        },
        ["required"] = new[] { "reason" }
    };

    private static readonly ChatTool EscalationTool = new()
    {
        Type = "function",
        Function = new ChatToolFunction
        {
            Name = EscalationToolName,
            Description = "Hands the conversation to a PIPDC administrator. Call it ONLY when: the user explicitly asks to speak to a human — a PIPDC staff member, an agent, an official — or asks to be contacted or visited in person; or the user raises a PIPDC organizational request that a real person must act on (paperwork, applications, appointments, complaints, transfers, or a definitive answer beyond the organizational profile) and redirecting to the website is not enough. Pass a concise reason describing what the user needs. NEVER call it for trivia, general knowledge, or other out-of-scope questions (those are redirected, not escalated), NEVER call it instead of searching the listings, and NEVER call it for a question the ORGANIZATION PROFILE already answers.",
            Parameters = EscalationFunctionParameters
        }
    };

    private sealed class ChatCompletionRequest
    {
        public string? Model { get; set; }
        public List<ChatMessage>? Messages { get; set; }
        public List<ChatTool>? Tools { get; set; }
        [JsonPropertyName("tool_choice")]
        public string? ToolChoice { get; set; }
        public double? Temperature { get; set; }
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        [JsonPropertyName("tool_calls")]
        public List<ChatToolCall>? ToolCalls { get; set; }
        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }
    }

    private sealed class ChatToolCall
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public ChatToolCallFunction? Function { get; set; }
    }

    private sealed class ChatToolCallFunction
    {
        public string? Name { get; set; }
        public string? Arguments { get; set; }
    }

    private sealed class ChatTool
    {
        public string? Type { get; set; }
        public ChatToolFunction? Function { get; set; }
    }

    private sealed class ChatToolFunction
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public object? Parameters { get; set; }
    }

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatResponseMessage? Message { get; set; }
    }

    private sealed class ChatResponseMessage
    {
        public string? Content { get; set; }
        [JsonPropertyName("tool_calls")]
        public List<ChatResponseToolCall>? ToolCalls { get; set; }
    }

    private sealed class ChatResponseToolCall
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public ChatResponseToolCallFunction? Function { get; set; }
    }

    private sealed class ChatResponseToolCallFunction
    {
        public string? Name { get; set; }
        public string? Arguments { get; set; }
    }
}