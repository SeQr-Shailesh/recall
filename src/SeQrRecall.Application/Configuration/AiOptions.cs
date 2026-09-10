namespace SeQrRecall.Application.Configuration;

/// <summary>
/// Settings for one summary vendor. Add a new property on <see cref="AiOptions"/> when a provider is added.
/// </summary>
public sealed class AiProviderEndpointOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }
}

/// <summary>
/// AI:Provider selects which vendor summarizes transcripts: Mock, OpenAI, or Gemini.
/// Each vendor has its own nested section so keys and models can stay configured together.
/// Flat AI:ApiKey / Model / BaseUrl remain as a fallback (user secrets).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "Mock";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public AiProviderEndpointOptions OpenAI { get; set; } = new();

    public AiProviderEndpointOptions Gemini { get; set; } = new();

    public AiProviderEndpointOptions ResolveActiveEndpoint()
    {
        AiProviderEndpointOptions named = IsGemini() ? Gemini : OpenAI;
        return new AiProviderEndpointOptions
        {
            ApiKey = FirstNonEmpty(named.ApiKey, ApiKey),
            Model = FirstNonEmpty(named.Model, Model),
            BaseUrl = FirstNonEmpty(named.BaseUrl, BaseUrl),
            TimeoutSeconds = named.TimeoutSeconds > 0 ? named.TimeoutSeconds : TimeoutSeconds
        };
    }

    public bool IsGemini()
    {
        return string.Equals(Provider, "Gemini", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(string? preferred, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
    }
}
