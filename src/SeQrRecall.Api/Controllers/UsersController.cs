using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeQrRecall.Application.Abstractions.Authentication;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Users;

namespace SeQrRecall.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IAuthenticationService _authentication;

    public UsersController(IAuthenticationService authentication)
    {
        _authentication = authentication;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetMe(CancellationToken cancellationToken)
    {
        UserDto user = await _authentication.GetCurrentUserAsync(cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        UserDto user = await _authentication.UpdateCurrentUserAsync(request, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }
}
