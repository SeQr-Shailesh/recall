using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Mapping;

internal static class NoteMapping
{
    public static NoteListDto ToListDto(this Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new NoteListDto
        {
            Id = note.Id,
            Title = note.Title,
            ShortSummary = note.ShortSummary,
            ProcessingStatus = note.ProcessingStatus,
            CreatedOn = note.CreatedOn,
            DurationSeconds = note.DurationSeconds
        };
    }

    public static NoteDetailsDto ToDetailsDto(this Note note, IReadOnlyList<NoteActionItem>? actionItems = null)
    {
        ArgumentNullException.ThrowIfNull(note);
        IReadOnlyList<NoteActionItem> items = actionItems ?? note.ActionItems.ToArray();
        return new NoteDetailsDto
        {
            Id = note.Id,
            Title = note.Title,
            ShortSummary = note.ShortSummary,
            FullSummary = note.FullSummary,
            Transcript = note.Transcript,
            ProcessingStatus = note.ProcessingStatus,
            ProcessingError = note.ProcessingError,
            CreatedOn = note.CreatedOn,
            CompletedOn = note.CompletedOn,
            DurationSeconds = note.DurationSeconds,
            DetectedLanguages = SplitLanguages(note.DetectedLanguages),
            ActionItems = items
                .OrderBy(item => item.CreatedOn)
                .Select(static item => item.ToDto())
                .ToArray(),
            HasAudio = !string.IsNullOrWhiteSpace(note.AudioFileUrl)
        };
    }

    public static ProcessingStatusDto ToStatusDto(this Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new ProcessingStatusDto
        {
            Id = note.Id,
            ProcessingStatus = note.ProcessingStatus,
            ProcessingError = note.ProcessingError,
            CompletedOn = note.CompletedOn
        };
    }

    public static CreateNoteResponse ToCreateResponse(this Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new CreateNoteResponse
        {
            Id = note.Id,
            ProcessingStatus = note.ProcessingStatus
        };
    }

    public static NoteActionItemDto ToDto(this NoteActionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new NoteActionItemDto
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
