using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;

namespace SeQrRecall.Application.Abstractions.Interactions;

public interface ICustomerInteractionsService
{
    Task<PagedResult<CustomerInteractionListDto>> ListForCustomerAsync(
        Guid customerId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateCustomerInteractionResponse> CreateAsync(
        CreateCustomerInteractionRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerInteractionDto> GetAsync(Guid interactionId, CancellationToken cancellationToken = default);

    Task<ProcessingStatusDto> GetStatusAsync(Guid interactionId, CancellationToken cancellationToken = default);

    Task<ProcessingStatusDto> UploadAudioAsync(
        Guid interactionId,
        Stream content,
        string? contentType,
        string? originalFileName,
        int? durationSeconds,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<CustomerInteractionAudioStream> OpenAudioAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default);

    Task<CustomerInteractionDto> UploadPhotoAsync(
        Guid interactionId,
        Stream content,
        string? contentType,
        string? originalFileName,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<CustomerInteractionPhotoStream> OpenPhotoAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default);
}
