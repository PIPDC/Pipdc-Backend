using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PIPDC.Application.AiChat;
using PIPDC.Domain.Common;

namespace PIPDC.Infrastructure.Gemini;

public sealed class GeminiClient(
    HttpClient http,
    IOptions<GeminiSettings> options,
    ILogger<GeminiClient> logger) : IGeminiClient
{
    private const string SearchToolName = "search_properties";
    private const string DevelopmentToolName = "search_developments";

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

        var request = new GenerateContentRequest
        {
            Contents = ToContents(history),
            SystemInstruction = ToContent(systemPrompt),
            Tools = new List<Tool> { SearchTool },
            ToolConfig = new ToolConfig
            {
                FunctionCallingConfig = new FunctionCallingConfig { Mode = "AUTO" }
            },
            GenerationConfig = new GenerationConfig { Temperature = 0.4, MaxOutputTokens = 800 }
        };

        var parsed = await ExecuteAsync(request, settings, ct);
        if (parsed.IsFailure)
            return Result<GeminiTurnResult>.Failure(parsed.Error);

        var content = parsed.Value.Candidates![0].Content;
        if (content is null)
            return Result<GeminiTurnResult>.Success(new GeminiTurnResult(null, null));

        var text = string.Join(string.Empty, content.Parts?.Select(p => p.Text).OfType<string>() ?? []);
        var functionCall = content.Parts?.Select(p => p.FunctionCall).FirstOrDefault(p => p is not null);

        if (functionCall is not null)
        {
            var json = functionCall.Args is { } args ? args.GetRawText() : "{}";
            return Result<GeminiTurnResult>.Success(
                new GeminiTurnResult(null, new GeminiToolCall(functionCall.Name ?? SearchToolName, json)));
        }

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

        var args = JsonDocument.Parse(toolCall.JsonArguments).RootElement;
        var contents = new List<Content>();
        contents.AddRange(ToContents(history));
        contents.Add(new Content
        {
            Role = "model",
            Parts = new List<Part> { new() { FunctionCall = new FunctionCall { Name = toolCall.Name, Args = args } } }
        });
        contents.Add(new Content
        {
            Role = "function",
            Parts = new List<Part>
            {
                new() { FunctionResponse = new FunctionResponse { Name = toolCall.Name, Response = new { result = toolResultText } } }
            }
        });

        var request = new GenerateContentRequest
        {
            Contents = contents,
            SystemInstruction = ToContent(systemPrompt),
            GenerationConfig = new GenerationConfig { Temperature = 0.4, MaxOutputTokens = 800 }
        };

        var parsed = await ExecuteAsync(request, settings, ct);
        if (parsed.IsFailure)
            return Result<string>.Failure(parsed.Error);

        var text = string.Join(string.Empty,
            parsed.Value.Candidates![0].Content?.Parts?.Select(p => p.Text).OfType<string>() ?? []);
        if (string.IsNullOrWhiteSpace(text))
            return Result<string>.Failure(
                Error.Failure("aichat.upstream", "The AI assistant produced no reply. Please try again."));

        return Result<string>.Success(text);
    }

    private async Task<Result<GenerateContentResponse>> ExecuteAsync(
        GenerateContentRequest request,
        GeminiSettings settings,
        CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            $"/v1beta/models/{settings.Model}:generateContent?key={settings.ApiKey}",
            request,
            JsonOpts,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Gemini returned {Status}: {Body}", response.StatusCode, body.Length > 500 ? body[..500] : body);
            return Result<GenerateContentResponse>.Failure(
                Error.Failure("aichat.upstream", "The AI assistant is temporarily unavailable. Please try again shortly."));
        }

        var parsed = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(JsonOpts, ct);
        return parsed is null || parsed.Candidates is not { Count: > 0 } || parsed.Candidates[0].Content is null
            ? Result<GenerateContentResponse>.Failure(
                Error.Failure("aichat.upstream", "The AI assistant is temporarily unavailable. Please try again shortly."))
            : Result<GenerateContentResponse>.Success(parsed);
    }

    private static List<Content> ToContents(IReadOnlyList<GeminiMessage> messages) =>
        messages.Select(m => ToContent(m.Content, m.Role)).ToList();

    private static Content ToContent(string text, string? role = null) =>
        new() { Role = role, Parts = new List<Part> { new() { Text = text } } };

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

    private static readonly Tool SearchTool = new()
    {
        FunctionDeclarations = new List<FunctionDeclaration>
        {
            new()
            {
                Name = SearchToolName,
                Description = "Searches the live PIPDC property database for listings matching the user's request and returns up to three matching properties. Call it as soon as the user provides ANY one concrete search detail — a location, an area or neighbourhood, a budget range, a number of bedrooms, or a listing type. For a neighbourhood like Rayfield passed as location/area, search immediately; do NOT ask for a city or state first. Filter strictly by the user's exact criteria: pass the EXACT bedroom count(s) requested, the stated location, budget and listing type; never search broader than the user asked for.",
                Parameters = SearchFunctionParameters
            },
            new()
            {
                Name = DevelopmentToolName,
                Description = "Looks up PIPDC's ongoing and upcoming property DEVELOPMENT projects — estates and developments still being planned, under construction or near completion — and returns up to six projects with name, status, progress percentage, location, expected completion, unit availability and the latest update. Call it when the user asks about ongoing, upcoming, future, in-progress or under-construction projects, development estates, or investment opportunities still being built. Do NOT use it for finished properties available now — those use search_properties.",
                Parameters = DevelopmentFunctionParameters
            }
        }
    };

    private sealed class GenerateContentRequest
    {
        public List<Content>? Contents { get; set; }
        public Content? SystemInstruction { get; set; }
        public List<Tool>? Tools { get; set; }
        public ToolConfig? ToolConfig { get; set; }
        public GenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GenerateContentResponse
    {
        public List<Candidate>? Candidates { get; set; }
    }

    private sealed class Candidate
    {
        public Content? Content { get; set; }
    }

    private sealed class Content
    {
        public string? Role { get; set; }
        public List<Part>? Parts { get; set; }
    }

    private sealed class Part
    {
        public string? Text { get; set; }
        public FunctionCall? FunctionCall { get; set; }
        public FunctionResponse? FunctionResponse { get; set; }
    }

    private sealed class FunctionCall
    {
        public string? Name { get; set; }
        public JsonElement? Args { get; set; }
    }

    private sealed class FunctionResponse
    {
        public string? Name { get; set; }
        public object? Response { get; set; }
    }

    private sealed class Tool
    {
        public List<FunctionDeclaration>? FunctionDeclarations { get; set; }
    }

    private sealed class FunctionDeclaration
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public object? Parameters { get; set; }
    }

    private sealed class ToolConfig
    {
        public FunctionCallingConfig? FunctionCallingConfig { get; set; }
    }

    private sealed class FunctionCallingConfig
    {
        public string? Mode { get; set; }
    }

    private sealed class GenerationConfig
    {
        public double? Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
    }
}