using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SeQrRecall.Api.Middleware;
using SeQrRecall.Application.Common.Constants;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SeQrRecall.UnitTests.Api;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task NotFound_returns_generic_404_envelope()
    {
        JsonElement root = await InvokeAndReadAsync(_ => throw new NotFoundException("Note", Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status404NotFound, _statusCode);
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
        Assert.Equal("The requested resource was not found.", root.GetProperty("message").GetString());
        Assert.DoesNotContain("Note", root.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_exception_returns_generic_500_without_type_name()
    {
        JsonElement root = await InvokeAndReadAsync(_ => throw new InvalidOperationException("secret internals"));

        Assert.Equal(StatusCodes.Status500InternalServerError, _statusCode);
        Assert.Equal("Unable to process request.", root.GetProperty("message").GetString());
        string json = root.GetRawText();
        Assert.DoesNotContain("InvalidOperationException", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret internals", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_exception_returns_400_with_errors()
    {
        JsonElement root = await InvokeAndReadAsync(_ =>
            throw new ValidationException("Invalid request.", ["FullName is required."]));

        Assert.Equal(StatusCodes.Status400BadRequest, _statusCode);
        Assert.Equal("Invalid request.", root.GetProperty("message").GetString());
        Assert.Equal("FullName is required.", root.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task Unauthorized_exception_returns_401_envelope()
    {
        JsonElement root = await InvokeAndReadAsync(_ => throw new UnauthorizedException());

        Assert.Equal(StatusCodes.Status401Unauthorized, _statusCode);
        Assert.Equal("Authentication is required.", root.GetProperty("message").GetString());
    }

    private int _statusCode;

    private async Task<JsonElement> InvokeAndReadAsync(RequestDelegate next)
    {
        ExceptionHandlingMiddleware middleware = new(next, NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Items[HttpHeaderNames.CorrelationId] = "test-correlation";

        await middleware.InvokeAsync(context);

        _statusCode = context.Response.StatusCode;
        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body, Encoding.UTF8);
        string json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
