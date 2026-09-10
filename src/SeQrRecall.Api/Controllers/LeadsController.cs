using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Application.Abstractions.Leads;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Leads;
using SeQrRecall.Application.Dtos.Notes;

namespace SeQrRecall.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly ILeadsService _leads;

    public LeadsController(ILeadsService leads)
    {
        _leads = leads;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LeadListDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<LeadListDto>>>> List(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<LeadListDto> page = await _leads.ListAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<LeadListDto>>.Ok(page));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateLeadResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CreateLeadResponse>>> Create(CancellationToken cancellationToken)
    {
        CreateLeadResponse created = await _leads.CreateAsync(cancellationToken);
        return Ok(ApiResponse<CreateLeadResponse>.Ok(created));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LeadDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LeadDetailsDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        LeadDetailsDto lead = await _leads.GetAsync(id, cancellationToken);
        return Ok(ApiResponse<LeadDetailsDto>.Ok(lead));
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ProcessingStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProcessingStatusDto>>> GetStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        ProcessingStatusDto status = await _leads.GetStatusAsync(id, cancellationToken);
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
        ProcessingStatusDto status = await _leads.UploadAudioAsync(
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
        LeadAudioStream audio = await _leads.OpenAudioAsync(id, cancellationToken);
        return File(audio.Stream, audio.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/photo")]
    [EnableRateLimiting(RateLimitPolicyNames.Upload)]
    [RequestSizeLimit(UploadsOptions.HardCeilingBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadsOptions.HardCeilingBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<LeadDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LeadDetailsDto>>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new ValidationException("A photo file is required.", ["file is required."]);
        }

        await using Stream stream = file.OpenReadStream();
        LeadDetailsDto lead = await _leads.UploadPhotoAsync(
            id,
            stream,
            file.ContentType,
            file.FileName,
            file.Length,
            cancellationToken);
        return Ok(ApiResponse<LeadDetailsDto>.Ok(lead));
    }

    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken cancellationToken)
    {
        LeadPhotoStream photo = await _leads.OpenPhotoAsync(id, cancellationToken);
        return File(photo.Stream, photo.ContentType);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _leads.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(data: null, message: "Lead deleted."));
    }
}
