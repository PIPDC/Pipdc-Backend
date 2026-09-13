namespace PIPDC.Infrastructure.Gemini;

public sealed class GeminiSettings
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-2.0-flash";
}