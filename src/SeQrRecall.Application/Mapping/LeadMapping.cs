using SeQrRecall.Application.Dtos.Leads;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Mapping;

internal static class LeadMapping
{
    public static LeadListDto ToListDto(this Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);
        return new LeadListDto
        {
            Id = lead.Id,
            Title = lead.Title,
            ShortSummary = lead.ShortSummary,
            ProcessingStatus = lead.ProcessingStatus,
            CreatedOn = lead.CreatedOn,
            DurationSeconds = lead.DurationSeconds,
            HasPhoto = !string.IsNullOrWhiteSpace(lead.PhotoUrl)
        };
    }

    public static LeadDetailsDto ToDetailsDto(this Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);
        return new LeadDetailsDto
        {
            Id = lead.Id,
            Title = lead.Title,
            ShortSummary = lead.ShortSummary,
            FullSummary = lead.FullSummary,
            Transcript = lead.Transcript,
            ProcessingStatus = lead.ProcessingStatus,
            ProcessingError = lead.ProcessingError,
            CreatedOn = lead.CreatedOn,
            CompletedOn = lead.CompletedOn,
            DurationSeconds = lead.DurationSeconds,
            DetectedLanguages = SplitLanguages(lead.DetectedLanguages),
            ActionItems = lead.ActionItems
                .OrderBy(item => item.CreatedOn)
                .Select(static item => item.ToDto())
                .ToArray(),
            HasAudio = !string.IsNullOrWhiteSpace(lead.AudioFileUrl),
            HasPhoto = !string.IsNullOrWhiteSpace(lead.PhotoUrl)
        };
    }

    public static ProcessingStatusDto ToStatusDto(this Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);
        return new ProcessingStatusDto
        {
            Id = lead.Id,
            ProcessingStatus = lead.ProcessingStatus,
            ProcessingError = lead.ProcessingError,
            CompletedOn = lead.CompletedOn
        };
    }

    public static CreateLeadResponse ToCreateResponse(this Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);
        return new CreateLeadResponse
        {
            Id = lead.Id,
            ProcessingStatus = lead.ProcessingStatus
        };
    }

    public static LeadActionItemDto ToDto(this LeadActionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new LeadActionItemDto
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
