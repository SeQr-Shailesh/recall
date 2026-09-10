using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Application.Abstractions.Customers;
using SeQrRecall.Application.Abstractions.Interactions;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Application.Dtos.Interactions;

namespace SeQrRecall.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomersService _customers;
    private readonly ICustomerInteractionsService _interactions;

    public CustomersController(ICustomersService customers, ICustomerInteractionsService interactions)
    {
        _customers = customers;
        _interactions = interactions;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> List(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<CustomerDto> page = await _customers.ListAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(page));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        CustomerDto created = await _customers.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<CustomerDto>.Ok(created));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        CustomerDto customer = await _customers.GetAsync(id, cancellationToken);
        return Ok(ApiResponse<CustomerDto>.Ok(customer));
    }

    [HttpGet("{id:guid}/interactions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerInteractionListDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerInteractionListDto>>>> ListInteractions(
        Guid id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<CustomerInteractionListDto> page = await _interactions.ListForCustomerAsync(
            id,
            request,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<CustomerInteractionListDto>>.Ok(page));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        CustomerDto customer = await _customers.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<CustomerDto>.Ok(customer));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _customers.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(data: null, message: "Customer deleted."));
    }

    [HttpPost("{id:guid}/photo")]
    [EnableRateLimiting(RateLimitPolicyNames.Upload)]
    [RequestSizeLimit(UploadsOptions.HardCeilingBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadsOptions.HardCeilingBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new ValidationException("A photo file is required.", ["file is required."]);
        }

        await using Stream stream = file.OpenReadStream();
        CustomerDto customer = await _customers.UploadPhotoAsync(
            id,
            stream,
            file.ContentType,
            file.FileName,
            file.Length,
            cancellationToken);
        return Ok(ApiResponse<CustomerDto>.Ok(customer));
    }

    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken cancellationToken)
    {
        CustomerPhotoStream photo = await _customers.OpenPhotoAsync(id, cancellationToken);
        return File(photo.Stream, photo.ContentType);
    }
}
