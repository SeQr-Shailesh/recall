using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Application.Abstractions.Notes;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Notes;

namespace SeQrRecall.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes")]
public sealed class NotesController : ControllerBase
{
    private readonly INotesService _notes;

    public NotesController(INotesService notes)
    {
        _notes = notes;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NoteListDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NoteListDto>>>> List(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<NoteListDto> page = await _notes.ListAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<NoteListDto>>.Ok(page));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateNoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CreateNoteResponse>>> Create(CancellationToken cancellationToken)
    {
        CreateNoteResponse created = await _notes.CreateAsync(cancellationToken);
        return Ok(ApiResponse<CreateNoteResponse>.Ok(created));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NoteDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NoteDetailsDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        NoteDetailsDto note = await _notes.GetAsync(id, cancellationToken);
        return Ok(ApiResponse<NoteDetailsDto>.Ok(note));
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ProcessingStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProcessingStatusDto>>> GetStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        ProcessingStatusDto status = await _notes.GetStatusAsync(id, cancellationToken);
        return Ok(ApiResponse<ProcessingStatusDto>.Ok(status));
    }

    [HttpPost("{id:guid}/audio")]
    [EnableRateLimiting(RateLimitPolicyNames.Upload)]
    [RequestSizeLimit(UploadsOptions.HardCeilingBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadsOptions.HardCeilingBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProcessingStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProcessingStatusDto>>> UploadAudio(
        Guid id,
        IFormFile? file,
        [FromForm] int? durationSeconds,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new ValidationException("An audio file is required.", ["file is required."]);
        }

        await using Stream stream = file.OpenReadStream();
        ProcessingStatusDto status = await _notes.UploadAudioAsync(
            id,
            stream,
            file.ContentType,
            file.FileName,
            durationSeconds,
            file.Length,
            cancellationToken);
        return Ok(ApiResponse<ProcessingStatusDto>.Ok(status));
    }

    [HttpGet("{id:guid}/audio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAudio(Guid id, CancellationToken cancellationToken)
    {
        NoteAudioStream audio = await _notes.OpenAudioAsync(id, cancellationToken);
        return File(audio.Stream, audio.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _notes.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(data: null, message: "Note deleted."));
    }
}
