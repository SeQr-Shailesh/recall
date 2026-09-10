using System.Text.Json;

namespace SeQrRecall.Infrastructure.Ai;

internal sealed class GeminiGenerateRequest
{
    public GeminiContent? SystemInstruction { get; init; }

    public required IReadOnlyList<GeminiContent> Contents { get; init; }

    public required GeminiGenerationConfig GenerationConfig { get; init; }
}

internal sealed class GeminiContent
{
    public string? Role { get; init; }

    public required IReadOnlyList<GeminiPart> Parts { get; init; }
}

internal sealed class GeminiPart
{
    public string? Text { get; init; }
}

internal sealed class GeminiGenerationConfig
{
    public string ResponseMimeType { get; init; } = "application/json";

    public required JsonElement ResponseJsonSchema { get; init; }
}

internal sealed class GeminiGenerateResponse
{
    public IReadOnlyList<GeminiCandidate>? Candidates { get; set; }

    public GeminiPromptFeedback? PromptFeedback { get; set; }
}

internal sealed class GeminiCandidate
{
    public GeminiContent? Content { get; set; }

    public string? FinishReason { get; set; }
}

internal sealed class GeminiPromptFeedback
{
    public string? BlockReason { get; set; }
}

internal sealed class GeminiErrorResponse
{
    public GeminiErrorDetails? Error { get; set; }
}

internal sealed class GeminiErrorDetails
{
    public int? Code { get; set; }

    public string? Message { get; set; }

    public string? Status { get; set; }
}
