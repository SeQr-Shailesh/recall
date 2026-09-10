using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Infrastructure.Speech;
using Xunit;

namespace SeQrRecall.UnitTests.Speech;

public sealed class MockSpeechToTextServiceTests
{
    [Fact]
    public async Task Returns_the_labeled_english_transcript()
    {
        MockSpeechToTextService service = new();
        await using MemoryStream audio = new(new byte[] { 1, 2, 3 });

        SpeechTranscriptionResult result = await service.TranscribeAsync(new SpeechTranscriptionRequest
        {
            AudioStream = audio,
            ContentType = "audio/mpeg"
        });

        Assert.Equal(MockSpeechToTextService.ProviderName, result.ProviderName);
        Assert.Equal(MockSpeechToTextService.EnglishTranscript, result.EnglishTranscript);
        Assert.Equal(MockSpeechToTextService.DetectedLanguages, result.DetectedLanguages);
        Assert.Equal("Mock", result.ProviderName);
    }
}
