using Microsoft.AspNetCore.Http;
using SeQrRecall.Api.Middleware;
using SeQrRecall.Application.Common.Constants;
using Xunit;

namespace SeQrRecall.UnitTests.Api;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Generates_correlation_id_when_header_missing()
    {
        DefaultHttpContext context = new();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey(HttpHeaderNames.CorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[HttpHeaderNames.CorrelationId]));
    }

    [Fact]
    public async Task Reuses_incoming_correlation_id()
    {
        DefaultHttpContext context = new();
        context.Request.Headers[HttpHeaderNames.CorrelationId] = "incoming-id";
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("incoming-id", context.Response.Headers[HttpHeaderNames.CorrelationId]);
    }
}
