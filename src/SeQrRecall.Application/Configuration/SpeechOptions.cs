namespace SeQrRecall.Application.Configuration;

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public string Provider { get; set; } = "Mock";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.sarvam.ai";

    /// <summary>
    /// Sarvam model id. Default matches published Saaras v3.
    /// </summary>
    public string Model { get; set; } = "saaras:v3";

    /// <summary>
    /// Per-request HttpClient timeout in seconds. Batch jobs poll until BatchTimeoutSeconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    public int BatchPollIntervalSeconds { get; set; } = 5;

    public int BatchTimeoutSeconds { get; set; } = 600;
}
