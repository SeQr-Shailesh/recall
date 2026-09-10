using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Application.Abstractions.Interactions;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;

namespace SeQrRecall.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customer-interactions")]
public sealed class CustomerInteractionsController : ControllerBase
{
    private readonly ICustomerInteractionsService _interactions;

    public CustomerInteractionsController(ICustomerInteractionsService interactions)
    {
        _interactions = interactions;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateCustomerInteractionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CreateCustomerInteractionResponse>>> Create(
        [FromBody] CreateCustomerInteractionRequest request,
        CancellationToken cancellationToken)
    {
        CreateCustomerInteractionResponse created = await _interactions.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<CreateCustomerInteractionResponse>.Ok(created));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerInteractionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerInteractionDto>>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        CustomerInteractionDto interaction = await _interactions.GetAsync(id, cancellationToken);
        return Ok(ApiResponse<CustomerInteractionDto>.Ok(interaction));
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ProcessingStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProcessingStatusDto>>> GetStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        ProcessingStatusDto status = await _interactions.GetStatusAsync(id, cancellationToken);
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
        ProcessingStatusDto status = await _interactions.UploadAudioAsync(
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
        CustomerInteractionAudioStream audio = await _interactions.OpenAudioAsync(id, cancellationToken);
        return File(audio.Stream, audio.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/photo")]
    [EnableRateLimiting(RateLimitPolicyNames.Upload)]
    [RequestSizeLimit(UploadsOptions.HardCeilingBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadsOptions.HardCeilingBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CustomerInteractionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerInteractionDto>>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new ValidationException("A photo file is required.", ["file is required."]);
        }

        await using Stream stream = file.OpenReadStream();
        CustomerInteractionDto interaction = await _interactions.UploadPhotoAsync(
            id,
            stream,
            file.ContentType,
            file.FileName,
            file.Length,
            cancellationToken);
        return Ok(ApiResponse<CustomerInteractionDto>.Ok(interaction));
    }

    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken cancellationToken)
    {
        CustomerInteractionPhotoStream photo = await _interactions.OpenPhotoAsync(id, cancellationToken);
        return File(photo.Stream, photo.ContentType);
    }
}
