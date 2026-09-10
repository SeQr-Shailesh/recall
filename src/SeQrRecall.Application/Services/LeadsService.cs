using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Leads;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Customers;
using SeQrRecall.Application.Dtos.Leads;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Mapping;
using SeQrRecall.Application.Notes;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Services;

public sealed class LeadsService : ILeadsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly IBackgroundJobQueue _jobs;
    private readonly UploadsOptions _uploads;
    private readonly ILogger<LeadsService> _logger;

    public LeadsService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files,
        IBackgroundJobQueue jobs,
        IOptions<UploadsOptions> uploads,
        ILogger<LeadsService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _jobs = jobs;
        _uploads = uploads.Value;
        _logger = logger;
    }

    public async Task<PagedResult<LeadListDto>> ListAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        (IReadOnlyList<Lead> leads, int totalCount) = await _db.GetLeadsPageForUserAsync(
            userId,
            request.PageNumber,
            request.PageSize,
            request.Search,
            cancellationToken);

        IReadOnlyList<LeadListDto> items = leads.Select(static lead => lead.ToListDto()).ToArray();
        return PagedResult<LeadListDto>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<CreateLeadResponse> CreateAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Lead lead = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProcessingStatus = ProcessingStatus.Draft
        };
        _db.Add(lead);
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "LeadCreated",
            EntityType = "Lead",
            EntityId = lead.Id,
            Details = "Draft lead created.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} created lead {LeadId}", userId, lead.Id);
        return lead.ToCreateResponse();
    }

    public async Task<LeadDetailsDto> GetAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: true, cancellationToken);
        return lead.ToDetailsDto();
    }

    public async Task<ProcessingStatusDto> GetStatusAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: false, cancellationToken);
        return lead.ToStatusDto();
    }

    public async Task<ProcessingStatusDto> UploadAudioAsync(
        Guid leadId,
        Stream content,
        string? contentType,
        string? originalFileName,
        int? durationSeconds,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: false, cancellationToken);

        if (lead.ProcessingStatus is ProcessingStatus.Uploading
            or ProcessingStatus.Uploaded
            or ProcessingStatus.Processing)
        {
            throw new ValidationException("Audio is already being processed for this lead.");
        }

        if (lead.ProcessingStatus is ProcessingStatus.Completed)
        {
            throw new ValidationException("This lead already has processed audio.");
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

        lead.ProcessingStatus = ProcessingStatus.Uploading;
        await _db.SaveChangesAsync(cancellationToken);

        string? previousFile = lead.AudioFileUrl;
        StoredFile stored;
        try
        {
            stored = await _files.SaveAsync(content, resolvedType, FileCategory.LeadAudio, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Audio storage failed for lead {LeadId}", lead.Id);
            lead.ProcessingStatus = ProcessingStatus.Failed;
            lead.ProcessingError = "The audio file could not be stored.";
            await _db.SaveChangesAsync(cancellationToken);
            throw new ValidationException("The audio file could not be stored.");
        }

        if (!string.IsNullOrWhiteSpace(previousFile))
        {
            try
            {
                await _files.DeleteAsync(previousFile, FileCategory.LeadAudio, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Previous audio file for lead {LeadId} could not be deleted", lead.Id);
            }
        }

        lead.AudioFileUrl = stored.StoredFileName;
        lead.DurationSeconds = durationSeconds;
        lead.ProcessingStatus = ProcessingStatus.Uploaded;
        lead.ProcessingError = null;
        lead.CompletedOn = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _jobs.QueueLeadProcessingAsync(lead.Id, cancellationToken);
        _logger.LogInformation("User {UserId} uploaded audio for lead {LeadId}", userId, lead.Id);
        return lead.ToStatusDto();
    }

    public async Task<LeadAudioStream> OpenAudioAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: false, cancellationToken);
        if (string.IsNullOrWhiteSpace(lead.AudioFileUrl))
        {
            throw new NotFoundException("Lead audio", leadId);
        }

        try
        {
            Stream stream = await _files.OpenReadAsync(lead.AudioFileUrl, FileCategory.LeadAudio, cancellationToken);
            return new LeadAudioStream
            {
                Stream = stream,
                ContentType = AudioUploadRules.ContentTypeFromStoredFileName(lead.AudioFileUrl),
                FileName = lead.AudioFileUrl
            };
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Lead audio", leadId);
        }
    }

    public async Task<LeadDetailsDto> UploadPhotoAsync(
        Guid leadId,
        Stream content,
        string? contentType,
        string? originalFileName,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: true, cancellationToken);

        if (contentLength <= 0)
        {
            throw new ValidationException("A photo file is required.", ["file is required."]);
        }

        if (contentLength > _uploads.PhotoMaxBytes)
        {
            throw new ValidationException("The photo is too large.");
        }

        string resolvedType = PhotoUploadRules.ResolveContentType(contentType, originalFileName);
        if (!PhotoUploadRules.IsAllowedContentType(resolvedType))
        {
            throw new ValidationException("Unsupported photo format.");
        }

        string? previousFile = lead.PhotoUrl;
        StoredFile stored;
        try
        {
            stored = await _files.SaveAsync(content, resolvedType, FileCategory.LeadPhoto, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Photo storage failed for lead {LeadId}", lead.Id);
            throw new ValidationException("The photo could not be stored.");
        }

        if (!string.IsNullOrWhiteSpace(previousFile))
        {
            try
            {
                await _files.DeleteAsync(previousFile, FileCategory.LeadPhoto, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Previous photo for lead {LeadId} could not be deleted", lead.Id);
            }
        }

        lead.PhotoUrl = stored.StoredFileName;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} uploaded a photo for lead {LeadId}", userId, lead.Id);
        return lead.ToDetailsDto();
    }

    public async Task<LeadPhotoStream> OpenPhotoAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: false, cancellationToken);
        if (string.IsNullOrWhiteSpace(lead.PhotoUrl))
        {
            throw new NotFoundException("Lead photo", leadId);
        }

        try
        {
            Stream stream = await _files.OpenReadAsync(lead.PhotoUrl, FileCategory.LeadPhoto, cancellationToken);
            return new LeadPhotoStream
            {
                Stream = stream,
                ContentType = PhotoUploadRules.ContentTypeFromStoredFileName(lead.PhotoUrl),
                FileName = lead.PhotoUrl
            };
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Lead photo", leadId);
        }
    }

    public async Task DeleteAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Lead lead = await GetOwnedLeadAsync(leadId, includeActionItems: false, cancellationToken);
        lead.IsDeleted = true;
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "LeadDeleted",
            EntityType = "Lead",
            EntityId = lead.Id,
            Details = "Lead soft-deleted.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} deleted lead {LeadId}", userId, lead.Id);
    }

    private async Task<Lead> GetOwnedLeadAsync(Guid leadId, bool includeActionItems, CancellationToken cancellationToken)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Lead? lead = includeActionItems
            ? await _db.GetLeadWithActionItemsForUserAsync(userId, leadId, cancellationToken)
            : await _db.GetLeadByIdForUserAsync(userId, leadId, cancellationToken);

        if (lead is null)
        {
            throw new NotFoundException("Lead", leadId);
        }

        return lead;
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
