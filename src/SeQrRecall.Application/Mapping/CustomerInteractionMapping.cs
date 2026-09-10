using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Mapping;

internal static class CustomerInteractionMapping
{
    public static CustomerInteractionListDto ToListDto(this CustomerInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        return new CustomerInteractionListDto
        {
            Id = interaction.Id,
            CustomerId = interaction.CustomerId,
            ShortSummary = interaction.ShortSummary,
            ProcessingStatus = interaction.ProcessingStatus,
            InteractionDate = interaction.InteractionDate,
            HasPhoto = !string.IsNullOrWhiteSpace(interaction.PhotoUrl)
        };
    }

    public static CustomerInteractionDto ToDto(this CustomerInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        return new CustomerInteractionDto
        {
            Id = interaction.Id,
            CustomerId = interaction.CustomerId,
            CustomerName = interaction.Customer?.Name ?? string.Empty,
            CompanyName = interaction.Customer?.CompanyName,
            ShortSummary = interaction.ShortSummary,
            FullSummary = interaction.FullSummary,
            Transcript = interaction.Transcript,
            ProcessingStatus = interaction.ProcessingStatus,
            ProcessingError = interaction.ProcessingError,
            InteractionDate = interaction.InteractionDate,
            CreatedOn = interaction.CreatedOn,
            CompletedOn = interaction.CompletedOn,
            DurationSeconds = interaction.DurationSeconds,
            DetectedLanguages = SplitLanguages(interaction.DetectedLanguages),
            ActionItems = interaction.ActionItems
                .OrderBy(item => item.CreatedOn)
                .Select(static item => item.ToDto())
                .ToArray(),
            HasAudio = !string.IsNullOrWhiteSpace(interaction.AudioFileUrl),
            HasPhoto = !string.IsNullOrWhiteSpace(interaction.PhotoUrl)
        };
    }

    public static CreateCustomerInteractionResponse ToCreateResponse(this CustomerInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        return new CreateCustomerInteractionResponse
        {
            Id = interaction.Id,
            CustomerId = interaction.CustomerId,
            ProcessingStatus = interaction.ProcessingStatus
        };
    }

    public static ProcessingStatusDto ToStatusDto(this CustomerInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        return new ProcessingStatusDto
        {
            Id = interaction.Id,
            ProcessingStatus = interaction.ProcessingStatus,
            ProcessingError = interaction.ProcessingError,
            CompletedOn = interaction.CompletedOn
        };
    }

    public static CustomerInteractionActionItemDto ToDto(this CustomerInteractionActionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new CustomerInteractionActionItemDto
        {
            Id = item.Id,
            Description = item.Description,
            DueDate = item.DueDate,
            IsCompleted = item.IsCompleted,
            Kind = item.Kind
        };
    }

    private static IReadOnlyList<string> SplitLanguages(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
