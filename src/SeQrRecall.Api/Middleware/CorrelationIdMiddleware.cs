using SeQrRecall.Application.Common.Constants;
using Serilog.Context;

namespace SeQrRecall.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = context.Request.Headers[HttpHeaderNames.CorrelationId].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[HttpHeaderNames.CorrelationId] = correlationId;
        context.Items[HttpHeaderNames.CorrelationId] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
