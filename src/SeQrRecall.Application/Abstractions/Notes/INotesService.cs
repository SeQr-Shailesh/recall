using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Notes;

namespace SeQrRecall.Application.Abstractions.Notes;

public interface INotesService
{
    Task<PagedResult<NoteListDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<CreateNoteResponse> CreateAsync(CancellationToken cancellationToken = default);

    Task<NoteDetailsDto> GetAsync(Guid noteId, CancellationToken cancellationToken = default);

    Task<ProcessingStatusDto> GetStatusAsync(Guid noteId, CancellationToken cancellationToken = default);

    Task<ProcessingStatusDto> UploadAudioAsync(
        Guid noteId,
        Stream content,
        string? contentType,
        string? originalFileName,
        int? durationSeconds,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<NoteAudioStream> OpenAudioAsync(Guid noteId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid noteId, CancellationToken cancellationToken = default);
}
