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

public sealed class GeminiSummaryServiceTests
{
    [Fact]
    public async Task Maps_structured_json_to_summary_result()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, GenerateBody(SummaryJson())));
        GeminiSummaryService service = Create(handler);

        AiSummaryResult result = await service.SummarizeAsync(
            "I had a meeting with Rajesh today. He liked our inventory system. He asked me to send the quotation by Friday.");

        Assert.Equal("Meeting with Rajesh", result.Title);
        Assert.Contains("inventory", result.ShortSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quotation", result.ActionItems[0].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ActionItems[0].DueDate);
        Assert.Contains(result.ImportantEntities, entity => entity.Name == "Rajesh");
        CapturedRequest capture = Assert.Single(handler.Captures);
        Assert.Equal("test-key", capture.ApiKey);
        Assert.Equal("/v1beta/models/gemini-3.5-flash:generateContent", capture.Path);
        Assert.Contains("responseMimeType", capture.Body, StringComparison.Ordinal);
        Assert.Contains("application/json", capture.Body, StringComparison.Ordinal);
        Assert.Contains("responseJsonSchema", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Do not invent", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Rajesh", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("key=", capture.Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthorized_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.Unauthorized, """
            {"error":{"code":401,"message":"API key not valid","status":"UNAUTHENTICATED"}}
            """));
        GeminiSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Equal(GeminiSummaryService.ProviderName, exception.ProviderName);
        Assert.Contains("authentication", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("API key not valid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rate_limit_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.TooManyRequests, """
            {"error":{"code":429,"message":"Resource exhausted","status":"RESOURCE_EXHAUSTED"}}
            """));
        GeminiSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("rate limited", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_envelope_json_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Text(HttpStatusCode.OK, "<html>nope</html>"));
        GeminiSummaryService service = Create(handler);

        await Assert.ThrowsAsync<ExternalProviderException>(() => service.SummarizeAsync("Hello"));
    }

    [Fact]
    public async Task Empty_candidates_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, """{"candidates":[]}"""));
        GeminiSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("no candidates", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Safety_block_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, """
            {"candidates":[{"finishReason":"SAFETY","content":{"parts":[{"text":""}]}}]}
            """));
        GeminiSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("blocked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Timeout_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => throw new TaskCanceledException("timed out"));
        GeminiSummaryService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => service.SummarizeAsync("Hello"));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_summary_rejects_missing_title()
    {
        ExternalProviderException exception = Assert.Throws<ExternalProviderException>(() =>
            GeminiSummaryService.ParseSummary("""{"title":"","shortSummary":"s","summary":"s","actionItems":[],"followUpItems":[],"importantEntities":[]}"""));

        Assert.Contains("required fields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_summary_rejects_invalid_json()
    {
        ExternalProviderException exception = Assert.Throws<ExternalProviderException>(() =>
            GeminiSummaryService.ParseSummary("not-json"));

        Assert.Contains("invalid summary JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_summary_maps_iso_due_date_and_ignores_relative_words()
    {
        AiSummaryResult result = GeminiSummaryService.ParseSummary(
            """
            {
              "title": "Quote",
              "shortSummary": "Send the quote.",
              "summary": "Send the quotation.",
              "actionItems": [
                { "description": "Send quote", "dueDate": "2026-08-22" },
                { "description": "Call back Friday", "dueDate": "Friday" }
              ],
              "followUpItems": [],
              "importantEntities": []
            }
            """);

        Assert.Equal(2, result.ActionItems.Count);
        Assert.NotNull(result.ActionItems[0].DueDate);
        Assert.Equal(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero).Date, result.ActionItems[0].DueDate!.Value.Date);
        Assert.Null(result.ActionItems[1].DueDate);
    }

    private static GeminiSummaryService Create(RecordingHandler handler)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/")
        };
        AiOptions options = new()
        {
            Provider = "Gemini",
            ApiKey = "test-key",
            Model = "gemini-3.5-flash",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
        };
        return new GeminiSummaryService(client, Options.Create(options), NullLogger<GeminiSummaryService>.Instance);
    }

    private static string SummaryJson()
    {
        return """
            {"title":"Meeting with Rajesh","shortSummary":"Rajesh liked the inventory system.","summary":"I had a meeting with Rajesh today. He liked our inventory system. He asked me to send the quotation by Friday.","actionItems":[{"description":"Send the quotation by Friday","dueDate":null}],"followUpItems":[],"importantEntities":[{"name":"Rajesh","type":"person"}]}
            """;
    }

    private static string GenerateBody(string contentJson)
    {
        string escaped = JsonSerializer.Serialize(contentJson);
        return "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"role\":\"model\",\"parts\":[{\"text\":" + escaped + "}]}}]}";
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
            string? apiKey = request.Headers.TryGetValues(GeminiSummaryService.ApiKeyHeaderName, out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;
            Captures.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                body,
                apiKey));
            return _onSend(request, body);
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string Body, string? ApiKey);
}
