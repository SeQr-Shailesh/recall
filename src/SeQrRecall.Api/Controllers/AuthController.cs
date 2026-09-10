using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Application.Abstractions.Authentication;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Auth;

namespace SeQrRecall.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authentication;

    public AuthController(IAuthenticationService authentication)
    {
        _authentication = authentication;
    }

    [AllowAnonymous]
    [HttpPost("send-otp")]
    [EnableRateLimiting(RateLimitPolicyNames.OtpSend)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> SendOtp(
        [FromBody] SendOtpRequest request,
        CancellationToken cancellationToken)
    {
        await _authentication.SendOtpAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(new { sent = true }, "A verification code has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("verify-otp")]
    [EnableRateLimiting(RateLimitPolicyNames.OtpVerify)]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VerifyOtpResponse>>> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        VerifyOtpResponse result = await _authentication.VerifyOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<VerifyOtpResponse>.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokensDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthTokensDto>>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        AuthTokensDto tokens = await _authentication.RefreshAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthTokensDto>.Ok(tokens));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await _authentication.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(data: null, message: "Signed out."));
    }
}
