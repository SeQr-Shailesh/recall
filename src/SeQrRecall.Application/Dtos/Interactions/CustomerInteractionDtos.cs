using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Dtos.Interactions;

public sealed class CreateCustomerInteractionRequest
{
    public required Guid CustomerId { get; init; }

    public DateTimeOffset? InteractionDate { get; init; }
}

public sealed class CreateCustomerInteractionResponse
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }
}

public sealed class CustomerInteractionListDto
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public string? ShortSummary { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }

    public DateTimeOffset InteractionDate { get; init; }

    public bool HasPhoto { get; init; }
}

public sealed class CustomerInteractionDto
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? CompanyName { get; init; }

    public string? ShortSummary { get; init; }

    public string? FullSummary { get; init; }

    public string? Transcript { get; init; }

    public ProcessingStatus ProcessingStatus { get; init; }

    public string? ProcessingError { get; init; }

    public DateTimeOffset InteractionDate { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? CompletedOn { get; init; }

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<string> DetectedLanguages { get; init; } = Array.Empty<string>();

    public IReadOnlyList<CustomerInteractionActionItemDto> ActionItems { get; init; } =
        Array.Empty<CustomerInteractionActionItemDto>();

    public bool HasAudio { get; init; }

    public bool HasPhoto { get; init; }
}

public sealed class CustomerInteractionActionItemDto
{
    public Guid Id { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset? DueDate { get; init; }

    public bool IsCompleted { get; init; }

    public ActionItemKind Kind { get; init; }
}

public sealed class CustomerInteractionAudioStream
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}

public sealed class CustomerInteractionPhotoStream
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}
