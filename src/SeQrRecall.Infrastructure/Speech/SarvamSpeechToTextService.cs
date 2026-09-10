using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Notes;

namespace SeQrRecall.Infrastructure.Speech;

/// <summary>
/// Sarvam Saaras STT. Uses REST for short audio and the published Batch job API when duration exceeds 30 seconds.
/// Always requests mode=translate so the contract returns English.
/// Register with Speech:Provider = Sarvam.
/// </summary>
public sealed class SarvamSpeechToTextService : ISpeechToTextService
{
    public const string ProviderName = "Sarvam";
    public const string DefaultBaseUrl = "https://api.sarvam.ai/";
    public const string DefaultModel = "saaras:v3";
    public const string TranslateMode = "translate";
    public const string AutoLanguage = "unknown";
    public const string SubscriptionKeyHeader = "api-subscription-key";
    public const int RestMaxDurationSeconds = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly SpeechOptions _options;
    private readonly ILogger<SarvamSpeechToTextService> _logger;

    public SarvamSpeechToTextService(
        HttpClient httpClient,
        IOptions<SpeechOptions> options,
        ILogger<SarvamSpeechToTextService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Speech:ApiKey is required when Speech:Provider is Sarvam.");
        }
    }

    public async Task<SpeechTranscriptionResult> TranscribeAsync(
        SpeechTranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.AudioStream);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] audio = await ReadAllBytesAsync(request.AudioStream, cancellationToken);
        if (audio.Length == 0)
        {
            throw new ExternalProviderException(ProviderName, "Audio is empty.");
        }

        string contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : AudioUploadRules.NormalizeContentType(request.ContentType);
        string fileName = FileNameForContentType(contentType);

        if (request.DurationSeconds > RestMaxDurationSeconds)
        {
            _logger.LogInformation(
                "Using Sarvam Batch STT because duration {DurationSeconds}s exceeds the REST limit of {RestLimit}s",
                request.DurationSeconds,
                RestMaxDurationSeconds);
            return await TranscribeViaBatchAsync(audio, contentType, fileName, cancellationToken);
        }

        try
        {
            return await TranscribeViaRestAsync(audio, contentType, fileName, cancellationToken);
        }
        catch (ExternalProviderException exception) when (ShouldFallbackToBatch(exception, request.DurationSeconds))
        {
            _logger.LogInformation(
                exception,
                "Sarvam REST STT rejected the audio; retrying with the Batch API");
            return await TranscribeViaBatchAsync(audio, contentType, fileName, cancellationToken);
        }
    }

    public static string FileNameForContentType(string contentType)
    {
        return AudioUploadRules.NormalizeContentType(contentType) switch
        {
            "audio/mpeg" or "audio/mp3" => "audio.mp3",
            "audio/wav" or "audio/wave" or "audio/x-wav" => "audio.wav",
            "audio/webm" => "audio.webm",
            "audio/aac" => "audio.aac",
            "audio/ogg" => "audio.ogg",
            _ => "audio.m4a"
        };
    }

    private async Task<SpeechTranscriptionResult> TranscribeViaRestAsync(
        byte[] audio,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        using MultipartFormDataContent form = new();
        ByteArrayContent fileContent = new(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(ResolveModel()), "model");
        form.Add(new StringContent(TranslateMode), "mode");
        form.Add(new StringContent(AutoLanguage), "language_code");

        using HttpRequestMessage httpRequest = CreateSarvamRequest(HttpMethod.Post, "speech-to-text", form);
        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw MapFailure(response.StatusCode, body, allowBatchFallback: true);
        }

        return ParseTranscription(body, "REST");
    }

    private async Task<SpeechTranscriptionResult> TranscribeViaBatchAsync(
        byte[] audio,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(ResolveBatchTimeoutSeconds()));
        CancellationToken batchToken = timeoutCts.Token;

        try
        {
            string jobId = await InitiateJobAsync(batchToken);
            string uploadUrl = await GetUploadUrlAsync(jobId, fileName, batchToken);
            await UploadToSignedUrlAsync(uploadUrl, audio, contentType, batchToken);
            await StartJobAsync(jobId, batchToken);
            IReadOnlyList<string> outputFiles = await WaitForJobAsync(jobId, batchToken);
            string downloadUrl = await GetDownloadUrlAsync(jobId, outputFiles[0], batchToken);
            string body = await DownloadResultAsync(downloadUrl, batchToken);
            return ParseTranscription(body, "Batch");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new ExternalProviderException(ProviderName, "The speech provider timed out.", exception);
        }
    }

    private async Task<string> InitiateJobAsync(CancellationToken cancellationToken)
    {
        SarvamJobInitRequest payload = new()
        {
            JobParameters = new SarvamJobParameters
            {
                Model = ResolveModel(),
                Mode = TranslateMode,
                LanguageCode = AutoLanguage
            }
        };

        using HttpRequestMessage httpRequest = CreateSarvamRequest(
            HttpMethod.Post,
            "speech-to-text/job/v1",
            JsonContent.Create(payload, options: JsonOptions));
        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response.StatusCode, body);

        SarvamJobInitResponse? parsed = JsonSerializer.Deserialize<SarvamJobInitResponse>(body, JsonOptions);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.JobId))
        {
            throw new ExternalProviderException(ProviderName, "Sarvam Batch job id was missing.");
        }

        return parsed.JobId;
    }

    private async Task<string> GetUploadUrlAsync(string jobId, string fileName, CancellationToken cancellationToken)
    {
        SarvamFilesRequest payload = new() { JobId = jobId, Files = [fileName] };
        using HttpRequestMessage httpRequest = CreateSarvamRequest(
            HttpMethod.Post,
            "speech-to-text/job/v1/upload-files",
            JsonContent.Create(payload, options: JsonOptions));
        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response.StatusCode, body);

        SarvamFilesUploadResponse? parsed = JsonSerializer.Deserialize<SarvamFilesUploadResponse>(body, JsonOptions);
        if (parsed?.UploadUrls is null
            || !parsed.UploadUrls.TryGetValue(fileName, out SarvamSignedUrl? signed)
            || string.IsNullOrWhiteSpace(signed.FileUrl))
        {
            throw new ExternalProviderException(ProviderName, "Sarvam Batch upload URL was missing.");
        }

        return signed.FileUrl;
    }

    private async Task UploadToSignedUrlAsync(
        string uploadUrl,
        byte[] audio,
        string contentType,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = new(HttpMethod.Put, uploadUrl)
        {
            Content = new ByteArrayContent(audio)
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        if (uploadUrl.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            httpRequest.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
        }

        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalProviderException(
                ProviderName,
                $"Sarvam Batch audio upload failed with HTTP {(int)response.StatusCode}.");
        }
    }

    private async Task StartJobAsync(string jobId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = CreateSarvamRequest(
            HttpMethod.Post,
            $"speech-to-text/job/v1/{Uri.EscapeDataString(jobId)}/start");
        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response.StatusCode, body);
    }

    private async Task<IReadOnlyList<string>> WaitForJobAsync(string jobId, CancellationToken cancellationToken)
    {
        int pollSeconds = _options.BatchPollIntervalSeconds > 0 ? _options.BatchPollIntervalSeconds : 5;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using HttpRequestMessage httpRequest = CreateSarvamRequest(
                HttpMethod.Get,
                $"speech-to-text/job/v1/{Uri.EscapeDataString(jobId)}/status");
            using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(response.StatusCode, body);

            SarvamJobStatusResponse? status = JsonSerializer.Deserialize<SarvamJobStatusResponse>(body, JsonOptions);
            string state = status?.JobState ?? string.Empty;
            if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new ExternalProviderException(
                    ProviderName,
                    "Sarvam Batch job failed.");
            }

            if (string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "PartiallyCompleted", StringComparison.OrdinalIgnoreCase))
            {
                List<string> outputs = status?.JobDetails?
                    .SelectMany(static detail => detail.Outputs ?? Array.Empty<SarvamJobFileName>())
                    .Select(static file => file.FileName)
                    .Where(static name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToList() ?? [];

                if (outputs.Count == 0)
                {
                    throw new ExternalProviderException(ProviderName, "Sarvam Batch job completed without output files.");
                }

                return outputs;
            }

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), cancellationToken);
        }
    }

    private async Task<string> GetDownloadUrlAsync(
        string jobId,
        string outputFileName,
        CancellationToken cancellationToken)
    {
        SarvamFilesRequest payload = new() { JobId = jobId, Files = [outputFileName] };
        using HttpRequestMessage httpRequest = CreateSarvamRequest(
            HttpMethod.Post,
            "speech-to-text/job/v1/download-files",
            JsonContent.Create(payload, options: JsonOptions));
        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response.StatusCode, body);

        SarvamFilesDownloadResponse? parsed = JsonSerializer.Deserialize<SarvamFilesDownloadResponse>(body, JsonOptions);
        if (parsed?.DownloadUrls is null
            || !parsed.DownloadUrls.TryGetValue(outputFileName, out SarvamSignedUrl? signed)
            || string.IsNullOrWhiteSpace(signed.FileUrl))
        {
            throw new ExternalProviderException(ProviderName, "Sarvam Batch download URL was missing.");
        }

        return signed.FileUrl;
    }

    private async Task<string> DownloadResultAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = new(HttpMethod.Get, downloadUrl);
        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalProviderException(
                ProviderName,
                $"Sarvam Batch result download failed with HTTP {(int)response.StatusCode}.");
        }

        return body;
    }

    private SpeechTranscriptionResult ParseTranscription(string body, string path)
    {
        SarvamTranscriptionResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SarvamTranscriptionResponse>(body, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ExternalProviderException(ProviderName, "Sarvam returned invalid JSON.", exception);
        }

        if (parsed is null)
        {
            throw new ExternalProviderException(ProviderName, "Sarvam returned invalid JSON.");
        }

        if (string.IsNullOrWhiteSpace(parsed.Transcript))
        {
            throw new ExternalProviderException(ProviderName, "Sarvam returned an empty transcript.");
        }

        string[] languages = string.IsNullOrWhiteSpace(parsed.LanguageCode)
            ? []
            : [parsed.LanguageCode];

        _logger.LogInformation(
            "Sarvam {Path} transcription succeeded. RequestId: {RequestId}. Language: {Language}",
            path,
            parsed.RequestId,
            parsed.LanguageCode);

        return new SpeechTranscriptionResult
        {
            EnglishTranscript = parsed.Transcript.Trim(),
            DetectedLanguages = languages,
            ProviderRawTranscript = parsed.Transcript.Trim(),
            ProviderName = ProviderName
        };
    }

    private HttpRequestMessage CreateSarvamRequest(HttpMethod method, string relativePath, HttpContent? content = null)
    {
        HttpRequestMessage httpRequest = new(method, relativePath) { Content = content };
        httpRequest.Headers.TryAddWithoutValidation(SubscriptionKeyHeader, _options.ApiKey);
        return httpRequest;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new ExternalProviderException(ProviderName, "The speech provider timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalProviderException(ProviderName, "The speech provider is unavailable.", exception);
        }
    }

    private void EnsureSuccess(HttpStatusCode statusCode, string body)
    {
        if ((int)statusCode is >= 200 and <= 299)
        {
            return;
        }

        throw MapFailure(statusCode, body, allowBatchFallback: false);
    }

    private ExternalProviderException MapFailure(HttpStatusCode statusCode, string body, bool allowBatchFallback)
    {
        string? code = null;
        try
        {
            SarvamErrorResponse? error = JsonSerializer.Deserialize<SarvamErrorResponse>(body, JsonOptions);
            code = error?.Error?.Code;
        }
        catch (JsonException)
        {
            // Provider error bodies are not always JSON. Status code is enough to classify.
        }

        string reason = statusCode switch
        {
            HttpStatusCode.Forbidden when string.Equals(code, "invalid_api_key_error", StringComparison.OrdinalIgnoreCase)
                => "authentication failed",
            HttpStatusCode.Forbidden => "authentication failed",
            HttpStatusCode.TooManyRequests => "rate limited",
            HttpStatusCode.ServiceUnavailable => "unavailable",
            HttpStatusCode.GatewayTimeout => "timed out",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity when allowBatchFallback
                && LooksLikeDurationLimit(body, code)
                => "audio exceeds REST duration limit",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "invalid request",
            _ => $"HTTP {(int)statusCode}"
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            reason = $"{reason} ({code})";
        }

        return new ExternalProviderException(ProviderName, $"Sarvam request failed: {reason}.");
    }

    private static bool ShouldFallbackToBatch(ExternalProviderException exception, int? durationSeconds)
    {
        if (durationSeconds > RestMaxDurationSeconds)
        {
            return true;
        }

        return exception.Message.Contains("duration", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeDurationLimit(string body, string? code)
    {
        if (code is not null
            && code.Contains("duration", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return body.Contains("duration", StringComparison.OrdinalIgnoreCase)
            || body.Contains("too long", StringComparison.OrdinalIgnoreCase)
            || body.Contains("30 second", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveModel()
    {
        return string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model.Trim();
    }

    private int ResolveBatchTimeoutSeconds()
    {
        return _options.BatchTimeoutSeconds > 0 ? _options.BatchTimeoutSeconds : 600;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
