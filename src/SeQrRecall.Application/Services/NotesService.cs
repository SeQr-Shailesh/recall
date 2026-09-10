using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Notes;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Mapping;
using SeQrRecall.Application.Notes;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Services;

public sealed class NotesService : INotesService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly IBackgroundJobQueue _jobs;
    private readonly UploadsOptions _uploads;
    private readonly ILogger<NotesService> _logger;

    public NotesService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files,
        IBackgroundJobQueue jobs,
        IOptions<UploadsOptions> uploads,
        ILogger<NotesService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _jobs = jobs;
        _uploads = uploads.Value;
        _logger = logger;
    }

    public async Task<PagedResult<NoteListDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        (IReadOnlyList<Note> notes, int totalCount) = await _db.GetNotesPageForUserAsync(
            userId,
            request.PageNumber,
            request.PageSize,
            request.Search,
            cancellationToken);

        IReadOnlyList<NoteListDto> items = notes.Select(static note => note.ToListDto()).ToArray();
        return PagedResult<NoteListDto>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<CreateNoteResponse> CreateAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Note note = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProcessingStatus = ProcessingStatus.Draft
        };
        _db.Add(note);
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "NoteCreated",
            EntityType = "Note",
            EntityId = note.Id,
            Details = "Draft note created.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} created note {NoteId}", userId, note.Id);
        return note.ToCreateResponse();
    }

    public async Task<NoteDetailsDto> GetAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        Note note = await GetOwnedNoteAsync(noteId, includeActionItems: true, cancellationToken);
        return note.ToDetailsDto();
    }

    public async Task<ProcessingStatusDto> GetStatusAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        Note note = await GetOwnedNoteAsync(noteId, includeActionItems: false, cancellationToken);
        return note.ToStatusDto();
    }

    public async Task<ProcessingStatusDto> UploadAudioAsync(
        Guid noteId,
        Stream content,
        string? contentType,
        string? originalFileName,
        int? durationSeconds,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Note note = await GetOwnedNoteAsync(noteId, includeActionItems: false, cancellationToken);

        if (note.ProcessingStatus is ProcessingStatus.Uploading
            or ProcessingStatus.Uploaded
            or ProcessingStatus.Processing)
        {
            throw new ValidationException("Audio is already being processed for this note.");
        }

        if (note.ProcessingStatus is ProcessingStatus.Completed)
        {
            throw new ValidationException("This note already has processed audio.");
        }

        if (contentLength <= 0)
        {
            throw new ValidationException("An audio file is required.", ["file is required."]);
        }

        if (contentLength > _uploads.AudioMaxBytes)
        {
            throw new ValidationException("The audio file is too large.");
        }

        string resolvedType = AudioUploadRules.ResolveContentType(contentType, originalFileName);
        if (!AudioUploadRules.IsAllowedContentType(resolvedType))
        {
            throw new ValidationException("Unsupported audio format.");
        }

        if (durationSeconds is < 0)
        {
            throw new ValidationException("Duration cannot be negative.", ["durationSeconds is invalid."]);
        }

        note.ProcessingStatus = ProcessingStatus.Uploading;
        await _db.SaveChangesAsync(cancellationToken);

        string? previousFile = note.AudioFileUrl;
        StoredFile stored;
        try
        {
            stored = await _files.SaveAsync(content, resolvedType, FileCategory.NoteAudio, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Audio storage failed for note {NoteId}", note.Id);
            note.ProcessingStatus = ProcessingStatus.Failed;
            note.ProcessingError = "The audio file could not be stored.";
            await _db.SaveChangesAsync(cancellationToken);
            throw new ValidationException("The audio file could not be stored.");
        }

        if (!string.IsNullOrWhiteSpace(previousFile))
        {
            try
            {
                await _files.DeleteAsync(previousFile, FileCategory.NoteAudio, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Previous audio file for note {NoteId} could not be deleted", note.Id);
            }
        }

        note.AudioFileUrl = stored.StoredFileName;
        note.DurationSeconds = durationSeconds;
        note.ProcessingStatus = ProcessingStatus.Uploaded;
        note.ProcessingError = null;
        note.CompletedOn = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _jobs.QueueNoteProcessingAsync(note.Id, cancellationToken);
        _logger.LogInformation("User {UserId} uploaded audio for note {NoteId}", userId, note.Id);
        return note.ToStatusDto();
    }

    public async Task<NoteAudioStream> OpenAudioAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        Note note = await GetOwnedNoteAsync(noteId, includeActionItems: false, cancellationToken);
        if (string.IsNullOrWhiteSpace(note.AudioFileUrl))
        {
            throw new NotFoundException("Note audio", noteId);
        }

        try
        {
            Stream stream = await _files.OpenReadAsync(note.AudioFileUrl, FileCategory.NoteAudio, cancellationToken);
            return new NoteAudioStream
            {
                Stream = stream,
                ContentType = AudioUploadRules.ContentTypeFromStoredFileName(note.AudioFileUrl),
                FileName = note.AudioFileUrl
            };
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Note audio", noteId);
        }
    }

    public async Task DeleteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Note note = await GetOwnedNoteAsync(noteId, includeActionItems: false, cancellationToken);
        note.IsDeleted = true;
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "NoteDeleted",
            EntityType = "Note",
            EntityId = note.Id,
            Details = "Note soft-deleted.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} deleted note {NoteId}", userId, note.Id);
    }

    private async Task<Note> GetOwnedNoteAsync(Guid noteId, bool includeActionItems, CancellationToken cancellationToken)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Note? note = includeActionItems
            ? await _db.GetNoteWithActionItemsForUserAsync(userId, noteId, cancellationToken)
            : await _db.GetNoteByIdForUserAsync(userId, noteId, cancellationToken);

        if (note is null)
        {
            throw new NotFoundException("Note", noteId);
        }

        return note;
    }

    private async Task<Guid> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException();
        }

        User? user = await _db.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException();
        }

        return user.Id;
    }
}
