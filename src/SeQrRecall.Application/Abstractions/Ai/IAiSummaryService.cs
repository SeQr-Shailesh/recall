namespace SeQrRecall.Application.Abstractions.Ai;

public sealed class AiActionItem
{
    public required string Description { get; init; }

    public DateTimeOffset? DueDate { get; init; }
}

public sealed class AiImportantEntity
{
    public required string Name { get; init; }

    public string? Type { get; init; }
}

/// <summary>
/// Structured AI output. Provider JSON is parsed and validated before this type is returned.
/// </summary>
public sealed class AiSummaryResult
{
    public required string Title { get; init; }

    public required string ShortSummary { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<AiActionItem> ActionItems { get; init; } = Array.Empty<AiActionItem>();

    public IReadOnlyList<AiActionItem> FollowUpItems { get; init; } = Array.Empty<AiActionItem>();

    public IReadOnlyList<AiImportantEntity> ImportantEntities { get; init; } = Array.Empty<AiImportantEntity>();
}

/// <summary>
/// LLM summarization provider. Never invent facts that are not present in the transcript.
/// </summary>
public interface IAiSummaryService
{
    Task<AiSummaryResult> SummarizeAsync(string englishTranscript, CancellationToken cancellationToken = default);
}
