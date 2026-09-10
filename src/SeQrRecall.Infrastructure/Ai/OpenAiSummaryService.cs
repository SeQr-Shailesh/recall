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
/// OpenAI Chat Completions summarizer with published json_schema structured outputs.
/// Always treats the input as English. Register with AI:Provider = OpenAI.
/// </summary>
public sealed class OpenAiSummaryService : IAiSummaryService
{
    public const string ProviderName = "OpenAI";
    public const string DefaultBaseUrl = "https://api.openai.com/v1/";
    public const string DefaultModel = "gpt-4o-mini";
    public const string CompletionsPath = "chat/completions";

    internal const string SystemPrompt =
        """
        You summarize English voice-note transcripts for SeQr Recall.
        The transcript is already English. Do not re-translate proper nouns into different English names.
        Be factual only. Do not invent commitments, deadlines, quantities, orders, or people.
        If the speaker said "might buy", do not write that they confirmed an order.
        Preserve names, companies, products, numbers, dates, and locations exactly as spoken.
        Do not create a due date unless an explicit calendar date was spoken. Relative words such as Friday without a date must use dueDate null.
        dueDate must be an ISO-8601 date or date-time, or null.
        title is a short headline. shortSummary is one or two sentences. summary is a concise factual recap.
        actionItems are commitments the speaker made. followUpItems are things to check later.
        importantEntities are people, companies, products, or places mentioned.
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
    private readonly ILogger<OpenAiSummaryService> _logger;

    public OpenAiSummaryService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiSummaryService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _endpoint = options.Value.ResolveActiveEndpoint();
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_endpoint.ApiKey))
        {
            throw new InvalidOperationException("AI:ApiKey is required when AI:Provider is OpenAI.");
        }
    }

    public async Task<AiSummaryResult> SummarizeAsync(
        string englishTranscript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishTranscript);
        cancellationToken.ThrowIfCancellationRequested();

        OpenAiChatRequest payload = new()
        {
            Model = ResolveModel(),
            Messages =
            [
                new OpenAiChatMessage { Role = "system", Content = SystemPrompt },
                new OpenAiChatMessage { Role = "user", Content = englishTranscript.Trim() }
            ],
            ResponseFormat = new OpenAiResponseFormat
            {
                JsonSchema = new OpenAiJsonSchema
                {
                    Name = "note_summary",
                    Strict = true,
                    Schema = SummarySchema
                }
            }
        };

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, CompletionsPath)
        {
            Content = JsonContent.Create(payload, options: ApiJson)
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _endpoint.ApiKey);

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
            throw new ExternalProviderException(ProviderName, "OpenAI returned invalid JSON.", exception);
        }

        if (parsed?.Choices is not { Count: > 0 } choices)
        {
            throw new ExternalProviderException(ProviderName, "OpenAI returned no choices.");
        }

        OpenAiChoiceMessage? message = choices[0].Message;
        if (message is null)
        {
            throw new ExternalProviderException(ProviderName, "OpenAI returned no choices.");
        }

        if (!string.IsNullOrWhiteSpace(message.Refusal))
        {
            throw new ExternalProviderException(ProviderName, "OpenAI refused to summarize the transcript.");
        }

        if (string.Equals(choices[0].FinishReason, "content_filter", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalProviderException(ProviderName, "OpenAI blocked the transcript.");
        }

        AiSummaryResult result = ParseSummary(message.Content);
        _logger.LogInformation("OpenAI summarization succeeded. Model: {Model}", ResolveModel());
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
            throw new ExternalProviderException(ProviderName, "OpenAI returned invalid summary JSON.", exception);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.ShortSummary)
            || string.IsNullOrWhiteSpace(payload.Summary))
        {
            throw new ExternalProviderException(ProviderName, "OpenAI summary is missing required fields.");
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
            throw new ExternalProviderException(ProviderName, "OpenAI returned an empty summary.");
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

        return new ExternalProviderException(ProviderName, $"OpenAI request failed: {reason}.");
    }

    private string ResolveModel()
    {
        return string.IsNullOrWhiteSpace(_endpoint.Model) ? DefaultModel : _endpoint.Model.Trim();
    }
}
