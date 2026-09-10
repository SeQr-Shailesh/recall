using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Interactions;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Customers;
using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Mapping;
using SeQrRecall.Application.Notes;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Application.Services;

public sealed class CustomerInteractionsService : ICustomerInteractionsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly IBackgroundJobQueue _jobs;
    private readonly UploadsOptions _uploads;
    private readonly ILogger<CustomerInteractionsService> _logger;

    public CustomerInteractionsService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files,
        IBackgroundJobQueue jobs,
        IOptions<UploadsOptions> uploads,
        ILogger<CustomerInteractionsService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _jobs = jobs;
        _uploads = uploads.Value;
        _logger = logger;
    }

    public async Task<PagedResult<CustomerInteractionListDto>> ListForCustomerAsync(
        Guid customerId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        await GetOwnedCustomerAsync(userId, customerId, cancellationToken);

        (IReadOnlyList<CustomerInteraction> interactions, int totalCount) =
            await _db.GetInteractionsPageForCustomerAsync(
                userId,
                customerId,
                request.PageNumber,
                request.PageSize,
                request.Search,
                cancellationToken);

        IReadOnlyList<CustomerInteractionListDto> items =
            interactions.Select(static interaction => interaction.ToListDto()).ToArray();
        return PagedResult<CustomerInteractionListDto>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<CreateCustomerInteractionResponse> CreateAsync(
        CreateCustomerInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CustomerId == Guid.Empty)
        {
            throw new ValidationException("A customer is required.", ["customerId is required."]);
        }

        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        await GetOwnedCustomerAsync(userId, request.CustomerId, cancellationToken);

        CustomerInteraction interaction = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CustomerId = request.CustomerId,
            InteractionDate = request.InteractionDate ?? DateTimeOffset.UtcNow,
            ProcessingStatus = ProcessingStatus.Draft
        };
        _db.Add(interaction);
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "CustomerInteractionCreated",
            EntityType = "CustomerInteraction",
            EntityId = interaction.Id,
            Details = "Draft customer interaction created.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "User {UserId} created interaction {InteractionId} for customer {CustomerId}",
            userId,
            interaction.Id,
            interaction.CustomerId);
        return interaction.ToCreateResponse();
    }

    public async Task<CustomerInteractionDto> GetAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        CustomerInteraction interaction = await GetOwnedInteractionAsync(
            interactionId,
            includeDetails: true,
            cancellationToken);
        return interaction.ToDto();
    }

    public async Task<ProcessingStatusDto> GetStatusAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        CustomerInteraction interaction = await GetOwnedInteractionAsync(
            interactionId,
            includeDetails: false,
            cancellationToken);
        return interaction.ToStatusDto();
    }

    public async Task<ProcessingStatusDto> UploadAudioAsync(
        Guid interactionId,
        Stream content,
        string? contentType,
        string? originalFileName,
        int? durationSeconds,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        CustomerInteraction interaction = await GetOwnedInteractionAsync(
            interactionId,
            includeDetails: false,
            cancellationToken);

        if (interaction.ProcessingStatus is ProcessingStatus.Uploading
            or ProcessingStatus.Uploaded
            or ProcessingStatus.Processing)
        {
            throw new ValidationException("Audio is already being processed for this conversation.");
        }

        if (interaction.ProcessingStatus is ProcessingStatus.Completed)
        {
            throw new ValidationException("This conversation already has processed audio.");
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

        interaction.ProcessingStatus = ProcessingStatus.Uploading;
        await _db.SaveChangesAsync(cancellationToken);

        string? previousFile = interaction.AudioFileUrl;
        StoredFile stored;
        try
        {
            stored = await _files.SaveAsync(content, resolvedType, FileCategory.InteractionAudio, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Audio storage failed for interaction {InteractionId}", interaction.Id);
            interaction.ProcessingStatus = ProcessingStatus.Failed;
            interaction.ProcessingError = "The audio file could not be stored.";
            await _db.SaveChangesAsync(cancellationToken);
            throw new ValidationException("The audio file could not be stored.");
        }

        if (!string.IsNullOrWhiteSpace(previousFile))
        {
            try
            {
                await _files.DeleteAsync(previousFile, FileCategory.InteractionAudio, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Previous audio file for interaction {InteractionId} could not be deleted",
                    interaction.Id);
            }
        }

        interaction.AudioFileUrl = stored.StoredFileName;
        interaction.DurationSeconds = durationSeconds;
        interaction.ProcessingStatus = ProcessingStatus.Uploaded;
        interaction.ProcessingError = null;
        interaction.CompletedOn = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _jobs.QueueCustomerInteractionProcessingAsync(interaction.Id, cancellationToken);
        _logger.LogInformation("User {UserId} uploaded audio for interaction {InteractionId}", userId, interaction.Id);
        return interaction.ToStatusDto();
    }

    public async Task<CustomerInteractionAudioStream> OpenAudioAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        CustomerInteraction interaction = await GetOwnedInteractionAsync(
            interactionId,
            includeDetails: false,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(interaction.AudioFileUrl))
        {
            throw new NotFoundException("Customer interaction audio", interactionId);
        }

        try
        {
            Stream stream = await _files.OpenReadAsync(
                interaction.AudioFileUrl,
                FileCategory.InteractionAudio,
                cancellationToken);
            return new CustomerInteractionAudioStream
            {
                Stream = stream,
                ContentType = AudioUploadRules.ContentTypeFromStoredFileName(interaction.AudioFileUrl),
                FileName = interaction.AudioFileUrl
            };
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Customer interaction audio", interactionId);
        }
    }

    public async Task<CustomerInteractionDto> UploadPhotoAsync(
        Guid interactionId,
        Stream content,
        string? contentType,
        string? originalFileName,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        CustomerInteraction interaction = await GetOwnedInteractionAsync(
            interactionId,
            includeDetails: true,
            cancellationToken);

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

        string? previousFile = interaction.PhotoUrl;
        StoredFile stored;
        try
        {
            stored = await _files.SaveAsync(content, resolvedType, FileCategory.InteractionPhoto, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Photo storage failed for interaction {InteractionId}", interaction.Id);
            throw new ValidationException("The photo could not be stored.");
        }

        if (!string.IsNullOrWhiteSpace(previousFile))
        {
            try
            {
                await _files.DeleteAsync(previousFile, FileCategory.InteractionPhoto, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Previous photo for interaction {InteractionId} could not be deleted",
                    interaction.Id);
            }
        }

        interaction.PhotoUrl = stored.StoredFileName;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} uploaded a photo for interaction {InteractionId}", userId, interaction.Id);
        return interaction.ToDto();
    }

    public async Task<CustomerInteractionPhotoStream> OpenPhotoAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        CustomerInteraction interaction = await GetOwnedInteractionAsync(
            interactionId,
            includeDetails: false,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(interaction.PhotoUrl))
        {
            throw new NotFoundException("Customer interaction photo", interactionId);
        }

        try
        {
            Stream stream = await _files.OpenReadAsync(
                interaction.PhotoUrl,
                FileCategory.InteractionPhoto,
                cancellationToken);
            return new CustomerInteractionPhotoStream
            {
                Stream = stream,
                ContentType = PhotoUploadRules.ContentTypeFromStoredFileName(interaction.PhotoUrl),
                FileName = interaction.PhotoUrl
            };
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Customer interaction photo", interactionId);
        }
    }

    private async Task<Customer> GetOwnedCustomerAsync(
        Guid userId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        Customer? customer = await _db.GetCustomerByIdForUserAsync(userId, customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("Customer", customerId);
        }

        return customer;
    }

    private async Task<CustomerInteraction> GetOwnedInteractionAsync(
        Guid interactionId,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        CustomerInteraction? interaction = includeDetails
            ? await _db.GetCustomerInteractionWithDetailsForUserAsync(userId, interactionId, cancellationToken)
            : await _db.GetCustomerInteractionByIdForUserAsync(userId, interactionId, cancellationToken);

        if (interaction is null)
        {
            throw new NotFoundException("Customer interaction", interactionId);
        }

        return interaction;
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
