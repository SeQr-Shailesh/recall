using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Dtos.Notes;

public sealed class NoteListDto
{
    public Guid Id { get; init; }

    public string? Title { get; init; }

    public string? ShortSummary { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public int? DurationSeconds { get; init; }
}
