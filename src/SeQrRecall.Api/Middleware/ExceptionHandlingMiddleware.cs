using System.Net;
using System.Text.Json;
using SeQrRecall.Api.Serialization;
using SeQrRecall.Application.Common.Constants;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;

namespace SeQrRecall.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        string correlationId = context.Items[HttpHeaderNames.CorrelationId] as string
            ?? context.TraceIdentifier;

        (HttpStatusCode statusCode, string message, IReadOnlyList<string> errors) = exception switch
        {
            ValidationException validation => (
                HttpStatusCode.BadRequest,
                validation.Message,
                validation.Errors),
            NotFoundException => (
                HttpStatusCode.NotFound,
                "The requested resource was not found.",
                Array.Empty<string>()),
            UnauthorizedException => (
                HttpStatusCode.Unauthorized,
                exception.Message,
                Array.Empty<string>()),
            ForbiddenAccessException forbidden => (
                HttpStatusCode.Forbidden,
                forbidden.Message,
                Array.Empty<string>()),
            ExternalProviderException => (
                HttpStatusCode.ServiceUnavailable,
                "Unable to process request.",
                Array.Empty<string>()),
            _ => (
                HttpStatusCode.InternalServerError,
                "Unable to process request.",
                Array.Empty<string>())
        };

        if ((int)statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Request failed. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}",
                (int)statusCode,
                correlationId);
        }
        else
        {
            _logger.LogWarning(
                "Request rejected. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}",
                (int)statusCode,
                correlationId);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers[HttpHeaderNames.CorrelationId] = correlationId;

        ApiResponse<object?> payload = ApiResponse.Fail(message, errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, ApiJsonSerializer.Options));
    }
}
