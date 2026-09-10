using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SeQrRecall.Api.Serialization;
using SeQrRecall.Application.Common.Constants;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;

namespace SeQrRecall.Api.RateLimiting;

public static class RateLimitRejectionWriter
{
    public static async ValueTask WriteAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext httpContext = context.HttpContext;
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        string correlationId = httpContext.Items[HttpHeaderNames.CorrelationId] as string
            ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.Headers[HttpHeaderNames.CorrelationId] = correlationId;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        ApiResponse<object?> payload = ApiResponse.Fail("Too many requests. Try again in a moment.");
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, ApiJsonSerializer.Options),
            cancellationToken);
    }

    public static string PartitionKey(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        string? userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static TimeSpan Window(int seconds)
    {
        int safe = seconds < 1 ? 60 : seconds;
        return TimeSpan.FromSeconds(safe);
    }

    public static int PermitLimit(int configured)
    {
        return configured < 1 ? 1 : configured;
    }

    public static RateLimitOptions Normalize(RateLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new RateLimitOptions
        {
            OtpSendPermitLimit = PermitLimit(options.OtpSendPermitLimit),
            OtpVerifyPermitLimit = PermitLimit(options.OtpVerifyPermitLimit),
            OtpWindowSeconds = options.OtpWindowSeconds < 1 ? 60 : options.OtpWindowSeconds,
            UploadPermitLimit = PermitLimit(options.UploadPermitLimit),
            UploadWindowSeconds = options.UploadWindowSeconds < 1 ? 60 : options.UploadWindowSeconds
        };
    }
}
