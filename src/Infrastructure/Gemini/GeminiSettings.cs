namespace PIPDC.Infrastructure.Gemini;

public sealed class GeminiSettings
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.6-flash";
}