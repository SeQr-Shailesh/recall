using SeQrRecall.Application.Abstractions.Ai;

namespace SeQrRecall.Infrastructure.Ai;

/// <summary>
/// Labeled mock summarizer. Does not call an LLM. Register with AI:Provider = Mock.
/// </summary>
public sealed class MockAiSummaryService : IAiSummaryService
{
    public const string ProviderName = "Mock";

    public Task<AiSummaryResult> SummarizeAsync(string englishTranscript, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishTranscript);
        cancellationToken.ThrowIfCancellationRequested();

        string trimmed = englishTranscript.Trim();
        string title = FirstSentence(trimmed, 80);
        string shortSummary = trimmed.Length <= 400 ? trimmed : trimmed[..400];

        List<AiActionItem> actionItems = [];
        foreach (string sentence in SplitSentences(trimmed))
        {
            if (sentence.Contains("asked me to", StringComparison.OrdinalIgnoreCase)
                || sentence.Contains("need to", StringComparison.OrdinalIgnoreCase)
                || sentence.Contains("follow up", StringComparison.OrdinalIgnoreCase))
            {
                actionItems.Add(new AiActionItem { Description = sentence });
            }
        }

        List<AiImportantEntity> entities = [];
        if (trimmed.Contains("Rajesh", StringComparison.OrdinalIgnoreCase))
        {
            entities.Add(new AiImportantEntity { Name = "Rajesh", Type = "person" });
        }

        if (trimmed.Contains("inventory system", StringComparison.OrdinalIgnoreCase))
        {
            entities.Add(new AiImportantEntity { Name = "inventory system", Type = "product" });
        }

        return Task.FromResult(new AiSummaryResult
        {
            Title = title,
            ShortSummary = shortSummary,
            Summary = trimmed,
            ActionItems = actionItems,
            FollowUpItems = [],
            ImportantEntities = entities
        });
    }

    private static string FirstSentence(string text, int maxLength)
    {
        int period = text.IndexOf('.', StringComparison.Ordinal);
        string sentence = period > 0 ? text[..period] : text;
        sentence = sentence.Trim();
        return sentence.Length <= maxLength ? sentence : sentence[..maxLength];
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        return text
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static sentence => sentence.Length > 0);
    }
}
