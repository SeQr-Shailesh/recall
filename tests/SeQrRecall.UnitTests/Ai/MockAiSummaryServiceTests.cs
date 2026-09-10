using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Infrastructure.Ai;
using SeQrRecall.Infrastructure.Speech;
using Xunit;

namespace SeQrRecall.UnitTests.Ai;

public sealed class MockAiSummaryServiceTests
{
    [Fact]
    public async Task Extracts_title_and_action_item_from_the_mock_transcript()
    {
        MockAiSummaryService service = new();
        AiSummaryResult result = await service.SummarizeAsync(MockSpeechToTextService.EnglishTranscript);

        Assert.Contains("Rajesh", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.ActionItems);
        Assert.Contains(
            result.ActionItems,
            item => item.Description.Contains("quotation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ImportantEntities, entity => entity.Name == "Rajesh");
    }
}
