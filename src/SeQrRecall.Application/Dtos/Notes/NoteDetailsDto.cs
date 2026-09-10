using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Dtos.Notes;

public sealed class NoteActionItemDto
{
    public Guid Id { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset? DueDate { get; init; }

    public bool IsCompleted { get; init; }

    public ActionItemKind Kind { get; init; }
}

public sealed class NoteDetailsDto
{
    public Guid Id { get; init; }

    public string? Title { get; init; }

    public string? ShortSummary { get; init; }

    public string? FullSummary { get; init; }

    public string? Transcript { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }

    public string? ProcessingError { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? CompletedOn { get; init; }

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<string> DetectedLanguages { get; init; } = Array.Empty<string>();

    public IReadOnlyList<NoteActionItemDto> ActionItems { get; init; } = Array.Empty<NoteActionItemDto>();

    public bool HasAudio { get; init; }
}
