using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.System;

namespace SeQrRecall.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController : ControllerBase
{
    private readonly IHostEnvironment _environment;

    public SystemController(IHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("version")]
    [ProducesResponseType(typeof(ApiResponse<SystemVersionDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SystemVersionDto>> GetVersion()
    {
        SystemVersionDto dto = new()
        {
            ApiVersion = "1.0",
            ApplicationVersion = GetApplicationVersion(),
            Environment = _environment.EnvironmentName
        };

        return Ok(ApiResponse<SystemVersionDto>.Ok(dto));
    }

    private static string GetApplicationVersion()
    {
        AssemblyInformationalVersionAttribute? informational = typeof(Program).Assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault();

        string? value = informational?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(value))
        {
            return typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        }

        int plus = value.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? value[..plus] : value;
    }
}
