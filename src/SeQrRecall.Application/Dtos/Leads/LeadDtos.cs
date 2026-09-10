using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Dtos.Leads;

public sealed class CreateLeadResponse
{
    public Guid Id { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }
}

public sealed class LeadListDto
{
    public Guid Id { get; init; }

    public string? Title { get; init; }

    public string? ShortSummary { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public int? DurationSeconds { get; init; }

    public bool HasPhoto { get; init; }
}

public sealed class LeadDetailsDto
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

    public IReadOnlyList<LeadActionItemDto> ActionItems { get; init; } = Array.Empty<LeadActionItemDto>();

    public bool HasAudio { get; init; }

    public bool HasPhoto { get; init; }
}

public sealed class LeadActionItemDto
{
    public Guid Id { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset? DueDate { get; init; }

    public bool IsCompleted { get; init; }

    public ActionItemKind Kind { get; init; }
}

public sealed class LeadAudioStream
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}

public sealed class LeadPhotoStream
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}
