namespace SeQrRecall.Application.Abstractions.Speech;

public sealed class SpeechTranscriptionRequest
{
    public required Stream AudioStream { get; init; }

    public required string ContentType { get; init; }

    public string? OriginalFileName { get; init; }

    public int? DurationSeconds { get; init; }
}

public sealed class SpeechTranscriptionResult
{
    public required string EnglishTranscript { get; init; }

    public IReadOnlyList<string> DetectedLanguages { get; init; } = Array.Empty<string>();

    public string? ProviderRawTranscript { get; init; }

    public string? ProviderName { get; init; }
}

/// <summary>
/// Speech-to-text provider. The application calls this contract; it never knows the concrete vendor.
/// </summary>
public interface ISpeechToTextService
{
    Task<SpeechTranscriptionResult> TranscribeAsync(
        SpeechTranscriptionRequest request,
        CancellationToken cancellationToken = default);
}
