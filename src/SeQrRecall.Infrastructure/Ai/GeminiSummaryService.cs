using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Configuration;

namespace SeQrRecall.Infrastructure.Ai;

/// <summary>
/// Gemini generateContent summarizer with published JSON-schema structured output.
/// Always treats the input as English. Register with AI:Provider = Gemini.
/// </summary>
public sealed class GeminiSummaryService : IAiSummaryService
{
    public const string ProviderName = "Gemini";
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
    public const string DefaultModel = "gemini-3.5-flash";
    public const string ApiKeyHeaderName = "x-goog-api-key";

    internal const string SystemPrompt = OpenAiSummaryService.SystemPrompt;

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ContentJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonElement SummarySchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["title", "shortSummary", "summary", "actionItems", "followUpItems", "importantEntities"],
          "properties": {
            "title": { "type": "string" },
            "shortSummary": { "type": "string" },
            "summary": { "type": "string" },
            "actionItems": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["description", "dueDate"],
                "properties": {
                  "description": { "type": "string" },
                  "dueDate": { "type": ["string", "null"] }
                }
              }
            },
            "followUpItems": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["description", "dueDate"],
                "properties": {
                  "description": { "type": "string" },
                  "dueDate": { "type": ["string", "null"] }
                }
              }
            },
            "importantEntities": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["name", "type"],
                "properties": {
                  "name": { "type": "string" },
                  "type": { "type": ["string", "null"] }
                }
              }
            }
          }
        }
        """);

    private readonly HttpClient _httpClient;
    private readonly AiProviderEndpointOptions _endpoint;
    private readonly ILogger<GeminiSummaryService> _logger;

    public GeminiSummaryService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiSummaryService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _endpoint = options.Value.ResolveActiveEndpoint();
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_endpoint.ApiKey))
        {
            throw new InvalidOperationException("AI:ApiKey is required when AI:Provider is Gemini.");
        }
    }

    public async Task<AiSummaryResult> SummarizeAsync(
        string englishTranscript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishTranscript);
        cancellationToken.ThrowIfCancellationRequested();

        GeminiGenerateRequest payload = new()
        {
            SystemInstruction = new GeminiContent
            {
                Parts = [new GeminiPart { Text = SystemPrompt }]
            },
            Contents =
            [
                new GeminiContent
                {
                    Role = "user",
                    Parts = [new GeminiPart { Text = englishTranscript.Trim() }]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseJsonSchema = SummarySchema
            }
        };

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, BuildGenerateContentUri())
        {
            Content = JsonContent.Create(payload, options: ApiJson)
        };
        httpRequest.Headers.TryAddWithoutValidation(ApiKeyHeaderName, _endpoint.ApiKey);

        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw MapFailure(response.StatusCode, body);
        }

        GeminiGenerateResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiGenerateResponse>(body, ApiJson);
        }
        catch (JsonException exception)
        {
            throw new ExternalProviderException(ProviderName, "Gemini returned invalid JSON.", exception);
        }

        if (!string.IsNullOrWhiteSpace(parsed?.PromptFeedback?.BlockReason))
        {
            throw new ExternalProviderException(ProviderName, "Gemini blocked the transcript.");
        }

        if (parsed?.Candidates is not { Count: > 0 } candidates)
        {
            throw new ExternalProviderException(ProviderName, "Gemini returned no candidates.");
        }

        GeminiCandidate candidate = candidates[0];
        if (IsBlockedFinishReason(candidate.FinishReason))
        {
            throw new ExternalProviderException(ProviderName, "Gemini blocked the transcript.");
        }

        string? text = candidate.Content?.Parts?
            .Select(static part => part.Text)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

        AiSummaryResult result = ParseSummary(text);
        _logger.LogInformation("Gemini summarization succeeded. Model: {Model}", ResolveModel());
        return result;
    }

    public static AiSummaryResult ParseSummary(string? content)
    {
        string json = UnwrapJson(content);
        OpenAiSummaryPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<OpenAiSummaryPayload>(json, ContentJson);
        }
        catch (JsonException exception)
        {
            throw new ExternalProviderException(ProviderName, "Gemini returned invalid summary JSON.", exception);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.ShortSummary)
            || string.IsNullOrWhiteSpace(payload.Summary))
        {
            throw new ExternalProviderException(ProviderName, "Gemini summary is missing required fields.");
        }

        return new AiSummaryResult
        {
            Title = payload.Title.Trim(),
            ShortSummary = payload.ShortSummary.Trim(),
            Summary = payload.Summary.Trim(),
            ActionItems = MapActions(payload.ActionItems),
            FollowUpItems = MapActions(payload.FollowUpItems),
            ImportantEntities = MapEntities(payload.ImportantEntities)
        };
    }

    private static IReadOnlyList<AiActionItem> MapActions(IReadOnlyList<OpenAiActionPayload>? items)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }

        List<AiActionItem> mapped = [];
        foreach (OpenAiActionPayload item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                continue;
            }

            mapped.Add(new AiActionItem
            {
                Description = item.Description.Trim(),
                DueDate = ParseDueDate(item.DueDate)
            });
        }

        return mapped;
    }

    private static IReadOnlyList<AiImportantEntity> MapEntities(IReadOnlyList<OpenAiEntityPayload>? items)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }

        List<AiImportantEntity> mapped = [];
        foreach (OpenAiEntityPayload item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            mapped.Add(new AiImportantEntity
            {
                Name = item.Name.Trim(),
                Type = string.IsNullOrWhiteSpace(item.Type) ? null : item.Type.Trim()
            });
        }

        return mapped;
    }

    private static DateTimeOffset? ParseDueDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
            out DateTimeOffset parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string UnwrapJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ExternalProviderException(ProviderName, "Gemini returned an empty summary.");
        }

        string trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLine = trimmed.IndexOf('\n');
            if (firstLine > 0)
            {
                trimmed = trimmed[(firstLine + 1)..];
            }

            int fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                trimmed = trimmed[..fence];
            }
        }

        return trimmed.Trim();
    }

    private Uri BuildGenerateContentUri()
    {
        string model = Uri.EscapeDataString(ResolveModel());
        string baseUrl = _httpClient.BaseAddress is null
            ? DefaultBaseUrl
            : _httpClient.BaseAddress.AbsoluteUri;
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        // Absolute URI: a relative path containing ':' is not a valid relative URI.
        return new Uri($"{baseUrl}models/{model}:generateContent", UriKind.Absolute);
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
            throw new ExternalProviderException(ProviderName, "The AI provider timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalProviderException(ProviderName, "The AI provider is unavailable.", exception);
        }
    }

    private static ExternalProviderException MapFailure(HttpStatusCode statusCode, string body)
    {
        string? code = null;
        try
        {
            GeminiErrorResponse? error = JsonSerializer.Deserialize<GeminiErrorResponse>(body, ApiJson);
            code = error?.Error?.Status ?? error?.Error?.Code?.ToString(CultureInfo.InvariantCulture);
        }
        catch (JsonException)
        {
            // Status code is enough when the error body is not JSON.
        }

        string reason = statusCode switch
        {
            HttpStatusCode.Unauthorized => "authentication failed",
            HttpStatusCode.Forbidden => "authentication failed",
            HttpStatusCode.TooManyRequests => "rate limited",
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout
                => "unavailable",
            HttpStatusCode.BadRequest => "invalid request",
            _ => $"HTTP {(int)statusCode}"
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            reason = $"{reason} ({code})";
        }

        return new ExternalProviderException(ProviderName, $"Gemini request failed: {reason}.");
    }

    private static bool IsBlockedFinishReason(string? finishReason)
    {
        if (string.IsNullOrWhiteSpace(finishReason))
        {
            return false;
        }

        return finishReason.Equals("SAFETY", StringComparison.OrdinalIgnoreCase)
            || finishReason.Equals("BLOCKLIST", StringComparison.OrdinalIgnoreCase)
            || finishReason.Equals("PROHIBITED_CONTENT", StringComparison.OrdinalIgnoreCase)
            || finishReason.Equals("SPII", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveModel()
    {
        if (string.IsNullOrWhiteSpace(_endpoint.Model))
        {
            return DefaultModel;
        }

        string model = _endpoint.Model.Trim();
        return model.Contains("gemini", StringComparison.OrdinalIgnoreCase) ? model : DefaultModel;
    }
}
