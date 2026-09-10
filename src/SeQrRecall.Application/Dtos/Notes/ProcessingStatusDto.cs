using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Dtos.Notes;

public sealed class CreateNoteResponse
{
    public Guid Id { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }
}

public sealed class ProcessingStatusDto
{
    public Guid Id { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }

    public string? ProcessingError { get; init; }

    public DateTimeOffset? CompletedOn { get; init; }
}
