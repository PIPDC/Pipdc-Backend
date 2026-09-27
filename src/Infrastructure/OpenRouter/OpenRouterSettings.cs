namespace PIPDC.Infrastructure.OpenRouter;

public sealed class OpenRouterSettings
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "google/gemini-2.5-flash";
}