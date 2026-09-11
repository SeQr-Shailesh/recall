using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Infrastructure.Ai;
using Xunit;

namespace SeQrRecall.UnitTests.Ai;

public sealed class QwenSummaryServiceTests
{
    [Fact]
    public async Task Maps_structured_json_to_summary_result()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, ChatBody(SummaryJson())));
        QwenSummaryService service = Create(handler);

        AiSummaryResult result = await service.SummarizeAsync(
            "I had a meeting with Rajesh today. He liked our inventory system. He asked me to send the quotation by Friday.");

        Assert.Equal("Meeting with Rajesh", result.Title);
        Assert.Contains("inventory", result.ShortSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quotation", result.ActionItems[0].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ActionItems[0].DueDate);
        Assert.Contains(result.ImportantEntities, entity => entity.Name == "Rajesh");
        CapturedRequest capture = Assert.Single(handler.Captures);
        Assert.Null(capture.Authorization);
        Assert.Equal("/v1/chat/completions", capture.Path);
        Assert.Contains("json_object", capture.Body, StringComparison.Ordinal);
        Assert.Contains("qwen3-4b", capture.Body, StringComparison.Ordinal);
        Assert.Contains("enable_thinking", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Do not invent", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Rajesh", capture.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_bearer_token_when_api_key_is_configured()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, ChatBody(SummaryJson())));
        QwenSummaryService service = Create(handler, apiKey: "qwen-secret");

        await service.SummarizeAsync("Hello");

        CapturedRequest capture = Assert.Single(handler.Captures);
        Assert.Equal("Bearer qwen-secret", capture.Authorization);
        Assert.DoesNotContain("qwen-secret", capture.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthorized_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.Unauthorized, """
            {"error":{"message":"Incorrect API key","type":"invalid_request_error","code":"invalid_api_key"}}
            """));
        QwenSummaryService service = Create(handler, apiKey: "qwen-secret");

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Equal(QwenSummaryService.ProviderName, exception.ProviderName);
        Assert.Contains("authentication", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("qwen-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Incorrect API key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rate_limit_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.TooManyRequests, """
            {"error":{"message":"Rate limit","type":"rate_limit_exceeded","code":"rate_limit_exceeded"}}
            """));
        QwenSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("rate limited", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_envelope_json_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Text(HttpStatusCode.OK, "<html>nope</html>"));
        QwenSummaryService service = Create(handler);

        await Assert.ThrowsAsync<ExternalProviderException>(() => service.SummarizeAsync("Hello"));
    }

    [Fact]
    public async Task Refusal_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, """
            {"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":null,"refusal":"I can't help with that."}}]}
            """));
        QwenSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("refused", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Timeout_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => throw new TaskCanceledException("timed out"));
        QwenSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_summary_rejects_missing_title()
    {
        ExternalProviderException exception = Assert.Throws<ExternalProviderException>(() =>
            QwenSummaryService.ParseSummary("""{"title":"","shortSummary":"s","summary":"s","actionItems":[],"followUpItems":[],"importantEntities":[]}"""));

        Assert.Contains("required fields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_summary_strips_thinking_tags()
    {
        AiSummaryResult result = QwenSummaryService.ParseSummary(
            """
            <think>Plan the summary first.</think>
            {"title":"Note","shortSummary":"Short.","summary":"Full.","actionItems":[],"followUpItems":[],"importantEntities":[]}
            """);

        Assert.Equal("Note", result.Title);
        Assert.Equal("Full.", result.Summary);
    }

    [Fact]
    public void Parse_summary_unwraps_fenced_json()
    {
        AiSummaryResult result = QwenSummaryService.ParseSummary(
            """
            ```json
            {"title":"Note","shortSummary":"Short.","summary":"Full.","actionItems":[],"followUpItems":[],"importantEntities":[]}
            ```
            """);

        Assert.Equal("Note", result.Title);
        Assert.Equal("Full.", result.Summary);
    }

    private static QwenSummaryService Create(RecordingHandler handler, string apiKey = "")
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("http://127.0.0.1:8000/v1/") };
        AiOptions options = new()
        {
            Provider = "Qwen",
            Qwen =
            {
                ApiKey = apiKey,
                Model = "qwen3-4b",
                BaseUrl = "http://127.0.0.1:8000/v1"
            }
        };
        return new QwenSummaryService(client, Options.Create(options), NullLogger<QwenSummaryService>.Instance);
    }

    private static string SummaryJson()
    {
        return """
            {"title":"Meeting with Rajesh","shortSummary":"Rajesh liked the inventory system.","summary":"I had a meeting with Rajesh today. He liked our inventory system. He asked me to send the quotation by Friday.","actionItems":[{"description":"Send the quotation by Friday","dueDate":null}],"followUpItems":[],"importantEntities":[{"name":"Rajesh","type":"person"}]}
            """;
    }

    private static string ChatBody(string contentJson)
    {
        string escaped = JsonSerializer.Serialize(contentJson);
        return "{\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":" + escaped + "}}]}";
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage Text(HttpStatusCode statusCode, string text)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(text, Encoding.UTF8, "text/html")
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _onSend;

        public RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> onSend)
        {
            _onSend = onSend;
        }

        public List<CapturedRequest> Captures { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            string? authorization = request.Headers.TryGetValues("Authorization", out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;
            Captures.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                body,
                authorization));
            return _onSend(request, body);
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string Body, string? Authorization);
}
