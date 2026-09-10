using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Leads;
using SeQrRecall.Application.Dtos.Notes;

namespace SeQrRecall.Application.Abstractions.Leads;

public interface ILeadsService
{
    Task<PagedResult<LeadListDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<CreateLeadResponse> CreateAsync(CancellationToken cancellationToken = default);

    Task<LeadDetailsDto> GetAsync(Guid leadId, CancellationToken cancellationToken = default);

    Task<ProcessingStatusDto> GetStatusAsync(Guid leadId, CancellationToken cancellationToken = default);

    Task<ProcessingStatusDto> UploadAudioAsync(
        Guid leadId,
        Stream content,
        string? contentType,
        string? originalFileName,
        int? durationSeconds,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<LeadAudioStream> OpenAudioAsync(Guid leadId, CancellationToken cancellationToken = default);

    Task<LeadDetailsDto> UploadPhotoAsync(
        Guid leadId,
        Stream content,
        string? contentType,
        string? originalFileName,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<LeadPhotoStream> OpenPhotoAsync(Guid leadId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid leadId, CancellationToken cancellationToken = default);
}
