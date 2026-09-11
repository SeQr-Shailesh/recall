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
/// Self-hosted Qwen3 Chat Completions summarizer (OpenAI-compatible /v1/chat/completions).
/// Always treats the input as English. Register with AI:Provider = Qwen.
/// Gemini remains available; only one provider is active at a time.
/// </summary>
public sealed class QwenSummaryService : IAiSummaryService
{
    public const string ProviderName = "Qwen";
    public const string DefaultBaseUrl = "http://127.0.0.1:8000/v1/";
    public const string DefaultModel = "qwen3-4b";
    public const string CompletionsPath = "chat/completions";

    internal const string SystemPrompt =
        OpenAiSummaryService.SystemPrompt
        + """

          Reply with a single JSON object only. Do not use markdown fences. Do not write reasoning or <think> tags.
          Required keys: title, shortSummary, summary, actionItems, followUpItems, importantEntities.
          actionItems and followUpItems items: description (string), dueDate (ISO-8601 string or null).
          importantEntities items: name (string), type (string or null).
          """;

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ContentJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AiProviderEndpointOptions _endpoint;
    private readonly ILogger<QwenSummaryService> _logger;

    public QwenSummaryService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<QwenSummaryService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _endpoint = options.Value.ResolveActiveEndpoint();
        _logger = logger;
    }

    public async Task<AiSummaryResult> SummarizeAsync(
        string englishTranscript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishTranscript);
        cancellationToken.ThrowIfCancellationRequested();

        QwenChatRequest payload = new()
        {
            Model = ResolveModel(),
            Messages =
            [
                new OpenAiChatMessage { Role = "system", Content = SystemPrompt },
                new OpenAiChatMessage { Role = "user", Content = englishTranscript.Trim() }
            ]
        };

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, CompletionsPath)
        {
            Content = JsonContent.Create(payload, options: ApiJson)
        };

        if (!string.IsNullOrWhiteSpace(_endpoint.ApiKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _endpoint.ApiKey);
        }

        using HttpResponseMessage response = await SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw MapFailure(response.StatusCode, body);
        }

        OpenAiChatResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(body, ApiJson);
        }
        catch (JsonException exception)
        {
            throw new ExternalProviderException(ProviderName, "Qwen returned invalid JSON.", exception);
        }

        if (parsed?.Choices is not { Count: > 0 } choices)
        {
            throw new ExternalProviderException(ProviderName, "Qwen returned no choices.");
        }

        OpenAiChoiceMessage? message = choices[0].Message;
        if (message is null)
        {
            throw new ExternalProviderException(ProviderName, "Qwen returned no choices.");
        }

        if (!string.IsNullOrWhiteSpace(message.Refusal))
        {
            throw new ExternalProviderException(ProviderName, "Qwen refused to summarize the transcript.");
        }

        if (string.Equals(choices[0].FinishReason, "content_filter", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalProviderException(ProviderName, "Qwen blocked the transcript.");
        }

        AiSummaryResult result = ParseSummary(message.Content);
        _logger.LogInformation("Qwen summarization succeeded. Model: {Model}", ResolveModel());
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
            throw new ExternalProviderException(ProviderName, "Qwen returned invalid summary JSON.", exception);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.ShortSummary)
            || string.IsNullOrWhiteSpace(payload.Summary))
        {
            throw new ExternalProviderException(ProviderName, "Qwen summary is missing required fields.");
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
            throw new ExternalProviderException(ProviderName, "Qwen returned an empty summary.");
        }

        string trimmed = StripThinking(content.Trim());
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

    private static string StripThinking(string content)
    {
        const string startTag = "<think>";
        const string endTag = "</think>";
        int startIndex = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return content;
        }

        int endIndex = content.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex >= 0)
        {
            return content.Remove(startIndex, endIndex + endTag.Length - startIndex).Trim();
        }

        int jsonStart = content.IndexOf('{');
        return jsonStart >= 0 ? content[jsonStart..] : content;
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
            OpenAiErrorResponse? error = JsonSerializer.Deserialize<OpenAiErrorResponse>(body, ApiJson);
            code = error?.Error?.Code ?? error?.Error?.Type;
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

        return new ExternalProviderException(ProviderName, $"Qwen request failed: {reason}.");
    }

    private string ResolveModel()
    {
        return string.IsNullOrWhiteSpace(_endpoint.Model) ? DefaultModel : _endpoint.Model.Trim();
    }
}
