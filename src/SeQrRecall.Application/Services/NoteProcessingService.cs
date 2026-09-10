using System.Text.Json;
using Microsoft.Extensions.Logging;
using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Notes;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Services;

public sealed class NoteProcessingService : INoteProcessingService
{
    private static readonly JsonSerializerOptions EntityJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly ISpeechToTextService _speech;
    private readonly IAiSummaryService _ai;
    private readonly ILogger<NoteProcessingService> _logger;

    public NoteProcessingService(
        IApplicationDbContext db,
        IFileStorageService files,
        ISpeechToTextService speech,
        IAiSummaryService ai,
        ILogger<NoteProcessingService> logger)
    {
        _db = db;
        _files = files;
        _speech = speech;
        _ai = ai;
        _logger = logger;
    }

    public async Task ProcessNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        Note? note = await _db.GetNoteWithActionItemsByIdAsync(noteId, cancellationToken);
        if (note is null)
        {
            _logger.LogWarning("Note {NoteId} was not found for processing", noteId);
            return;
        }

        if (note.ProcessingStatus is ProcessingStatus.Completed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(note.AudioFileUrl))
        {
            note.ProcessingStatus = ProcessingStatus.Failed;
            note.ProcessingError = "Audio is missing.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        note.ProcessingStatus = ProcessingStatus.Processing;
        note.ProcessingError = null;
        await _db.SaveChangesAsync(cancellationToken);

        SpeechTranscriptionResult transcription;
        try
        {
            await using Stream audio = await _files.OpenReadAsync(
                note.AudioFileUrl,
                FileCategory.NoteAudio,
                cancellationToken);
            transcription = await _speech.TranscribeAsync(
                new SpeechTranscriptionRequest
                {
                    AudioStream = audio,
                    ContentType = AudioUploadRules.ContentTypeFromStoredFileName(note.AudioFileUrl),
                    DurationSeconds = note.DurationSeconds
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Speech-to-text failed for note {NoteId}", noteId);
            note.ProcessingStatus = ProcessingStatus.Failed;
            note.ProcessingError = "Transcription failed. Please try uploading the audio again.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(transcription.EnglishTranscript))
        {
            note.ProcessingStatus = ProcessingStatus.Failed;
            note.ProcessingError = "Transcription failed. Please try uploading the audio again.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        note.Transcript = transcription.EnglishTranscript;
        note.DetectedLanguages = Truncate(string.Join(',', transcription.DetectedLanguages), 200);

        try
        {
            AiSummaryResult summary = await _ai.SummarizeAsync(transcription.EnglishTranscript, cancellationToken);
            ApplySummary(note, summary);
            note.ProcessingStatus = ProcessingStatus.Completed;
            note.CompletedOn = DateTimeOffset.UtcNow;
            note.ProcessingError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "AI summary failed for note {NoteId}", noteId);
            note.ProcessingStatus = ProcessingStatus.Completed;
            note.CompletedOn = DateTimeOffset.UtcNow;
            note.ProcessingError = "Summary could not be generated. The transcript is available.";
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Processed note {NoteId} with status {Status}. Provider: {Provider}",
            noteId,
            note.ProcessingStatus,
            transcription.ProviderName);
    }

    public async Task ProcessCustomerInteractionAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        CustomerInteraction? interaction = await _db.GetCustomerInteractionWithActionItemsByIdAsync(
            interactionId,
            cancellationToken);
        if (interaction is null)
        {
            _logger.LogWarning("Customer interaction {InteractionId} was not found for processing", interactionId);
            return;
        }

        if (interaction.ProcessingStatus is ProcessingStatus.Completed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(interaction.AudioFileUrl))
        {
            interaction.ProcessingStatus = ProcessingStatus.Failed;
            interaction.ProcessingError = "Audio is missing.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        interaction.ProcessingStatus = ProcessingStatus.Processing;
        interaction.ProcessingError = null;
        await _db.SaveChangesAsync(cancellationToken);

        SpeechTranscriptionResult transcription;
        try
        {
            await using Stream audio = await _files.OpenReadAsync(
                interaction.AudioFileUrl,
                FileCategory.InteractionAudio,
                cancellationToken);
            transcription = await _speech.TranscribeAsync(
                new SpeechTranscriptionRequest
                {
                    AudioStream = audio,
                    ContentType = AudioUploadRules.ContentTypeFromStoredFileName(interaction.AudioFileUrl),
                    DurationSeconds = interaction.DurationSeconds
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Speech-to-text failed for interaction {InteractionId}", interactionId);
            interaction.ProcessingStatus = ProcessingStatus.Failed;
            interaction.ProcessingError = "Transcription failed. Please try uploading the audio again.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(transcription.EnglishTranscript))
        {
            interaction.ProcessingStatus = ProcessingStatus.Failed;
            interaction.ProcessingError = "Transcription failed. Please try uploading the audio again.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        interaction.Transcript = transcription.EnglishTranscript;
        interaction.DetectedLanguages = Truncate(string.Join(',', transcription.DetectedLanguages), 200);

        try
        {
            AiSummaryResult summary = await _ai.SummarizeAsync(transcription.EnglishTranscript, cancellationToken);
            ApplyInteractionSummary(interaction, summary);
            interaction.ProcessingStatus = ProcessingStatus.Completed;
            interaction.CompletedOn = DateTimeOffset.UtcNow;
            interaction.ProcessingError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "AI summary failed for interaction {InteractionId}", interactionId);
            interaction.ProcessingStatus = ProcessingStatus.Completed;
            interaction.CompletedOn = DateTimeOffset.UtcNow;
            interaction.ProcessingError = "Summary could not be generated. The transcript is available.";
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Processed customer interaction {InteractionId} with status {Status}. Provider: {Provider}",
            interactionId,
            interaction.ProcessingStatus,
            transcription.ProviderName);
    }

    public async Task ProcessLeadAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        Lead? lead = await _db.GetLeadWithActionItemsByIdAsync(leadId, cancellationToken);
        if (lead is null)
        {
            _logger.LogWarning("Lead {LeadId} was not found for processing", leadId);
            return;
        }

        if (lead.ProcessingStatus is ProcessingStatus.Completed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(lead.AudioFileUrl))
        {
            lead.ProcessingStatus = ProcessingStatus.Failed;
            lead.ProcessingError = "Audio is missing.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        lead.ProcessingStatus = ProcessingStatus.Processing;
        lead.ProcessingError = null;
        await _db.SaveChangesAsync(cancellationToken);

        SpeechTranscriptionResult transcription;
        try
        {
            await using Stream audio = await _files.OpenReadAsync(
                lead.AudioFileUrl,
                FileCategory.LeadAudio,
                cancellationToken);
            transcription = await _speech.TranscribeAsync(
                new SpeechTranscriptionRequest
                {
                    AudioStream = audio,
                    ContentType = AudioUploadRules.ContentTypeFromStoredFileName(lead.AudioFileUrl),
                    DurationSeconds = lead.DurationSeconds
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Speech-to-text failed for lead {LeadId}", leadId);
            lead.ProcessingStatus = ProcessingStatus.Failed;
            lead.ProcessingError = "Transcription failed. Please try uploading the audio again.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(transcription.EnglishTranscript))
        {
            lead.ProcessingStatus = ProcessingStatus.Failed;
            lead.ProcessingError = "Transcription failed. Please try uploading the audio again.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        lead.Transcript = transcription.EnglishTranscript;
        lead.DetectedLanguages = Truncate(string.Join(',', transcription.DetectedLanguages), 200);

        try
        {
            AiSummaryResult summary = await _ai.SummarizeAsync(transcription.EnglishTranscript, cancellationToken);
            ApplyLeadSummary(lead, summary);
            lead.ProcessingStatus = ProcessingStatus.Completed;
            lead.CompletedOn = DateTimeOffset.UtcNow;
            lead.ProcessingError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "AI summary failed for lead {LeadId}", leadId);
            lead.ProcessingStatus = ProcessingStatus.Completed;
            lead.CompletedOn = DateTimeOffset.UtcNow;
            lead.ProcessingError = "Summary could not be generated. The transcript is available.";
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Processed lead {LeadId} with status {Status}. Provider: {Provider}",
            leadId,
            lead.ProcessingStatus,
            transcription.ProviderName);
    }

    private void ApplySummary(Note note, AiSummaryResult summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        foreach (NoteActionItem existing in note.ActionItems.ToList())
        {
            _db.Remove(existing);
        }

        note.ActionItems.Clear();

        string title = string.IsNullOrWhiteSpace(summary.Title)
            ? FallbackTitle(note.Transcript)
            : summary.Title.Trim();
        note.Title = Truncate(title, 200);
        note.ShortSummary = Truncate(summary.ShortSummary, 500);
        note.FullSummary = string.IsNullOrWhiteSpace(summary.Summary) ? null : summary.Summary.Trim();
        note.ImportantEntitiesJson = summary.ImportantEntities.Count == 0
            ? null
            : JsonSerializer.Serialize(
                summary.ImportantEntities.Select(static entity => new { entity.Name, entity.Type }),
                EntityJsonOptions);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        AddActionItems(note, summary.ActionItems, ActionItemKind.Action, now);
        AddActionItems(note, summary.FollowUpItems, ActionItemKind.FollowUp, now);
    }

    private void ApplyInteractionSummary(CustomerInteraction interaction, AiSummaryResult summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        foreach (CustomerInteractionActionItem existing in interaction.ActionItems.ToList())
        {
            _db.Remove(existing);
        }

        interaction.ActionItems.Clear();

        string shortSummary = !string.IsNullOrWhiteSpace(summary.ShortSummary)
            ? summary.ShortSummary.Trim()
            : !string.IsNullOrWhiteSpace(summary.Title)
                ? summary.Title.Trim()
                : FallbackTitle(interaction.Transcript, "Conversation");
        interaction.ShortSummary = Truncate(shortSummary, 500);
        interaction.FullSummary = string.IsNullOrWhiteSpace(summary.Summary) ? null : summary.Summary.Trim();
        interaction.ImportantEntitiesJson = summary.ImportantEntities.Count == 0
            ? null
            : JsonSerializer.Serialize(
                summary.ImportantEntities.Select(static entity => new { entity.Name, entity.Type }),
                EntityJsonOptions);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        AddInteractionActionItems(interaction, summary.ActionItems, ActionItemKind.Action, now);
        AddInteractionActionItems(interaction, summary.FollowUpItems, ActionItemKind.FollowUp, now);
    }

    private void ApplyLeadSummary(Lead lead, AiSummaryResult summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        foreach (LeadActionItem existing in lead.ActionItems.ToList())
        {
            _db.Remove(existing);
        }

        lead.ActionItems.Clear();

        string title = string.IsNullOrWhiteSpace(summary.Title)
            ? FallbackTitle(lead.Transcript, "Lead")
            : summary.Title.Trim();
        lead.Title = Truncate(title, 200);
        lead.ShortSummary = Truncate(summary.ShortSummary, 500);
        lead.FullSummary = string.IsNullOrWhiteSpace(summary.Summary) ? null : summary.Summary.Trim();
        lead.ImportantEntitiesJson = summary.ImportantEntities.Count == 0
            ? null
            : JsonSerializer.Serialize(
                summary.ImportantEntities.Select(static entity => new { entity.Name, entity.Type }),
                EntityJsonOptions);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        AddLeadActionItems(lead, summary.ActionItems, ActionItemKind.Action, now);
        AddLeadActionItems(lead, summary.FollowUpItems, ActionItemKind.FollowUp, now);
    }

    private void AddLeadActionItems(
        Lead lead,
        IReadOnlyList<AiActionItem> items,
        ActionItemKind kind,
        DateTimeOffset createdOn)
    {
        foreach (AiActionItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                continue;
            }

            LeadActionItem entity = new()
            {
                Id = Guid.NewGuid(),
                LeadId = lead.Id,
                Description = Truncate(item.Description.Trim(), 1000) ?? item.Description.Trim(),
                DueDate = item.DueDate,
                Kind = kind,
                CreatedOn = createdOn
            };
            lead.ActionItems.Add(entity);
            _db.Add(entity);
        }
    }

    private void AddActionItems(
        Note note,
        IReadOnlyList<AiActionItem> items,
        ActionItemKind kind,
        DateTimeOffset createdOn)
    {
        foreach (AiActionItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                continue;
            }

            NoteActionItem entity = new()
            {
                Id = Guid.NewGuid(),
                NoteId = note.Id,
                Description = Truncate(item.Description.Trim(), 1000) ?? item.Description.Trim(),
                DueDate = item.DueDate,
                Kind = kind,
                CreatedOn = createdOn
            };
            note.ActionItems.Add(entity);
            _db.Add(entity);
        }
    }

    private void AddInteractionActionItems(
        CustomerInteraction interaction,
        IReadOnlyList<AiActionItem> items,
        ActionItemKind kind,
        DateTimeOffset createdOn)
    {
        foreach (AiActionItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                continue;
            }

            CustomerInteractionActionItem entity = new()
            {
                Id = Guid.NewGuid(),
                InteractionId = interaction.Id,
                Description = Truncate(item.Description.Trim(), 1000) ?? item.Description.Trim(),
                DueDate = item.DueDate,
                Kind = kind,
                CreatedOn = createdOn
            };
            interaction.ActionItems.Add(entity);
            _db.Add(entity);
        }
    }

    private static string FallbackTitle(string? transcript, string fallback = "Voice note")
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return fallback;
        }

        string first = transcript.Trim();
        int period = first.IndexOf('.', StringComparison.Ordinal);
        if (period > 0 && period < 80)
        {
            first = first[..period];
        }

        return Truncate(first, 80) ?? fallback;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
