using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Infrastructure.Speech;
using Xunit;

namespace SeQrRecall.UnitTests.Speech;

public sealed class SarvamSpeechToTextServiceTests
{
    [Fact]
    public async Task Rest_translate_maps_english_transcript_and_language()
    {
        RecordingHandler handler = new((request, _) =>
        {
            Assert.Equal("/speech-to-text", request.RequestUri?.AbsolutePath);
            return Json(HttpStatusCode.OK, """
                {"request_id":"req-1","transcript":"I had a meeting with Rajesh today.","language_code":"hi-IN"}
                """);
        });
        SarvamSpeechToTextService service = Create(handler);

        SpeechTranscriptionResult result = await TranscribeAsync(service, contentType: "audio/mp4");

        Assert.Equal(SarvamSpeechToTextService.ProviderName, result.ProviderName);
        Assert.Equal("I had a meeting with Rajesh today.", result.EnglishTranscript);
        Assert.Equal(["hi-IN"], result.DetectedLanguages);
        Assert.Equal("I had a meeting with Rajesh today.", result.ProviderRawTranscript);
        Assert.Contains(handler.Captures, static capture =>
            capture.ApiKey == "test-key"
            && capture.Body.Contains("name=mode", StringComparison.Ordinal)
            && capture.Body.Contains("translate", StringComparison.Ordinal)
            && capture.Body.Contains("saaras:v3", StringComparison.Ordinal)
            && capture.Body.Contains("filename=audio.m4a", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rest_does_not_send_original_file_name()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, """
            {"transcript":"Hello.","language_code":"en-IN"}
            """));
        SarvamSpeechToTextService service = Create(handler);

        await TranscribeAsync(service, originalFileName: "customer-secret.m4a");

        Assert.DoesNotContain("customer-secret", handler.Captures[0].Body, StringComparison.Ordinal);
        Assert.Contains("audio.m4a", handler.Captures[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rest_403_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.Forbidden, """
            {"error":{"code":"invalid_api_key_error","message":"Invalid API key"}}
            """));
        SarvamSpeechToTextService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => TranscribeAsync(service));

        Assert.Equal(SarvamSpeechToTextService.ProviderName, exception.ProviderName);
        Assert.Contains("authentication", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rest_429_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.TooManyRequests, """
            {"error":{"code":"rate_limit_exceeded_error","message":"Slow down"}}
            """));
        SarvamSpeechToTextService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => TranscribeAsync(service));

        Assert.Contains("rate limited", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rest_invalid_json_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Text(HttpStatusCode.OK, "<html>nope</html>"));
        SarvamSpeechToTextService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => TranscribeAsync(service));

        Assert.Contains("invalid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rest_empty_transcript_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => Json(HttpStatusCode.OK, """{"transcript":"  ","language_code":"hi-IN"}"""));
        SarvamSpeechToTextService service = Create(handler);

        await Assert.ThrowsAsync<ExternalProviderException>(() => TranscribeAsync(service));
    }

    [Fact]
    public async Task Timeout_becomes_external_provider_exception()
    {
        RecordingHandler handler = new((_, _) => throw new TaskCanceledException("timed out"));
        SarvamSpeechToTextService service = Create(handler);

        ExternalProviderException exception = await Assert.ThrowsAsync<ExternalProviderException>(
            () => TranscribeAsync(service));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duration_over_rest_limit_uses_batch_api()
    {
        RecordingHandler handler = new((request, body) =>
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/speech-to-text/job/v1" && request.Method == HttpMethod.Post)
            {
                Assert.Contains("\"mode\":\"translate\"", body.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
                return Json(HttpStatusCode.Accepted, """{"job_id":"job-1","job_state":"Accepted","storage_container_type":"Azure_V1","job_parameters":{}}""");
            }

            if (path == "/speech-to-text/job/v1/upload-files")
            {
                return Json(HttpStatusCode.OK, """
                    {"job_id":"job-1","job_state":"Accepted","storage_container_type":"Azure_V1","upload_urls":{"audio.m4a":{"file_url":"https://example.blob.core.windows.net/container/audio.m4a?sas=1"}}}
                    """);
            }

            if (request.Method == HttpMethod.Put
                && request.RequestUri?.Host == "example.blob.core.windows.net")
            {
                Assert.False(request.Headers.Contains(SarvamSpeechToTextService.SubscriptionKeyHeader));
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (path == "/speech-to-text/job/v1/job-1/start")
            {
                return Json(HttpStatusCode.OK, """{"job_id":"job-1","job_state":"Running","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","storage_container_type":"Azure_V1"}""");
            }

            if (path == "/speech-to-text/job/v1/job-1/status")
            {
                return Json(HttpStatusCode.OK, """
                    {"job_id":"job-1","job_state":"Completed","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:01Z","storage_container_type":"Azure_V1","job_details":[{"state":"Success","outputs":[{"file_name":"0.json","file_id":"out-1"}]}]}
                    """);
            }

            if (path == "/speech-to-text/job/v1/download-files")
            {
                return Json(HttpStatusCode.OK, """
                    {"job_id":"job-1","job_state":"Completed","storage_container_type":"Azure_V1","download_urls":{"0.json":{"file_url":"https://example.blob.core.windows.net/container/0.json?sas=1"}}}
                    """);
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath.Contains("0.json", StringComparison.Ordinal) == true)
            {
                return Json(HttpStatusCode.OK, """{"request_id":"batch-1","transcript":"He asked me to send the quotation by Friday.","language_code":"en-IN"}""");
            }

            return Json(HttpStatusCode.NotFound, """{"error":{"code":"not_found_error","message":"unexpected call"}}""");
        });
        SarvamSpeechToTextService service = Create(handler);

        SpeechTranscriptionResult result = await TranscribeAsync(service, durationSeconds: 90);

        Assert.Equal("He asked me to send the quotation by Friday.", result.EnglishTranscript);
        Assert.Equal(["en-IN"], result.DetectedLanguages);
        Assert.DoesNotContain(handler.Captures, static capture => capture.Path == "/speech-to-text");
        Assert.Contains(handler.Captures, static capture => capture.Path == "/speech-to-text/job/v1");
        Assert.Contains(handler.Captures, static capture => capture.Method == "PUT");
    }

    [Fact]
    public async Task Rest_duration_rejection_falls_back_to_batch()
    {
        int restCalls = 0;
        RecordingHandler handler = new((request, _) =>
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/speech-to-text")
            {
                restCalls++;
                return Json(HttpStatusCode.BadRequest, """{"error":{"code":"invalid_request_error","message":"Audio duration exceeds 30 seconds"}}""");
            }

            if (path == "/speech-to-text/job/v1" && request.Method == HttpMethod.Post)
            {
                return Json(HttpStatusCode.Accepted, """{"job_id":"job-2","job_state":"Accepted","storage_container_type":"Local","job_parameters":{}}""");
            }

            if (path == "/speech-to-text/job/v1/upload-files")
            {
                return Json(HttpStatusCode.OK, """{"job_id":"job-2","job_state":"Accepted","storage_container_type":"Local","upload_urls":{"audio.m4a":{"file_url":"https://files.example/upload"}}}""");
            }

            if (request.Method == HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (path.EndsWith("/start", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"job_id":"job-2","job_state":"Running","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","storage_container_type":"Local"}""");
            }

            if (path.EndsWith("/status", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"job_id":"job-2","job_state":"Completed","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:01Z","storage_container_type":"Local","job_details":[{"state":"Success","outputs":[{"file_name":"0.json","file_id":"out"}]}]}""");
            }

            if (path == "/speech-to-text/job/v1/download-files")
            {
                return Json(HttpStatusCode.OK, """{"job_id":"job-2","job_state":"Completed","storage_container_type":"Local","download_urls":{"0.json":{"file_url":"https://files.example/0.json"}}}""");
            }

            if (request.RequestUri?.AbsolutePath == "/0.json")
            {
                return Json(HttpStatusCode.OK, """{"transcript":"The quotation is due Friday.","language_code":"en-IN"}""");
            }

            return Json(HttpStatusCode.NotFound, "{}");
        });
        SarvamSpeechToTextService service = Create(handler);

        SpeechTranscriptionResult result = await TranscribeAsync(service, durationSeconds: 20);

        Assert.Equal(1, restCalls);
        Assert.Equal("The quotation is due Friday.", result.EnglishTranscript);
    }

    [Fact]
    public void File_name_follows_content_type_not_client_name()
    {
        Assert.Equal("audio.m4a", SarvamSpeechToTextService.FileNameForContentType("audio/mp4"));
        Assert.Equal("audio.mp3", SarvamSpeechToTextService.FileNameForContentType("audio/mpeg"));
        Assert.Equal("audio.wav", SarvamSpeechToTextService.FileNameForContentType("audio/wav"));
    }

    private static SarvamSpeechToTextService Create(RecordingHandler handler)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("https://api.sarvam.ai/") };
        SpeechOptions options = new()
        {
            Provider = "Sarvam",
            ApiKey = "test-key",
            BaseUrl = "https://api.sarvam.ai",
            Model = "saaras:v3",
            BatchPollIntervalSeconds = 1,
            BatchTimeoutSeconds = 30
        };
        return new SarvamSpeechToTextService(client, Options.Create(options), NullLogger<SarvamSpeechToTextService>.Instance);
    }

    private static Task<SpeechTranscriptionResult> TranscribeAsync(
        SarvamSpeechToTextService service,
        string contentType = "audio/mp4",
        string? originalFileName = null,
        int? durationSeconds = null)
    {
        MemoryStream audio = new("fake-aac"u8.ToArray());
        return service.TranscribeAsync(new SpeechTranscriptionRequest
        {
            AudioStream = audio,
            ContentType = contentType,
            OriginalFileName = originalFileName,
            DurationSeconds = durationSeconds
        });
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
            string? apiKey = request.Headers.TryGetValues(SarvamSpeechToTextService.SubscriptionKeyHeader, out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;
            Captures.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                body,
                apiKey));
            return _onSend(request, body);
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string Body, string? ApiKey);
}
