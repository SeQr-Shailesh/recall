using SeQrRecall.Application.Abstractions.Speech;

namespace SeQrRecall.Infrastructure.Speech;

/// <summary>
/// Labeled mock STT. Does not call a vendor and must not be mistaken for production transcription.
/// Register with Speech:Provider = Mock.
/// </summary>
public sealed class MockSpeechToTextService : ISpeechToTextService
{
    public const string ProviderName = "Mock";

    public const string EnglishTranscript =
        "I had a meeting with Rajesh today. He liked our inventory system. He asked me to send the quotation by Friday.";

    public static readonly IReadOnlyList<string> DetectedLanguages = ["en-IN", "hi"];

    public async Task<SpeechTranscriptionResult> TranscribeAsync(
        SpeechTranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.AudioStream);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.AudioStream.CanRead)
        {
            byte[] buffer = new byte[4096];
            while (await request.AudioStream.ReadAsync(buffer, cancellationToken) > 0)
            {
            }
        }

        return new SpeechTranscriptionResult
        {
            EnglishTranscript = EnglishTranscript,
            DetectedLanguages = DetectedLanguages,
            ProviderName = ProviderName,
            ProviderRawTranscript = EnglishTranscript
        };
    }
}
