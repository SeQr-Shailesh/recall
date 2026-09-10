using System.Text.Json;

namespace SeQrRecall.Infrastructure.Ai;

internal sealed class OpenAiChatRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<OpenAiChatMessage> Messages { get; init; }

    public required OpenAiResponseFormat ResponseFormat { get; init; }
}

internal sealed class OpenAiChatMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}

internal sealed class OpenAiResponseFormat
{
    public string Type { get; init; } = "json_schema";

    public required OpenAiJsonSchema JsonSchema { get; init; }
}

internal sealed class OpenAiJsonSchema
{
    public required string Name { get; init; }

    public required bool Strict { get; init; }

    public required JsonElement Schema { get; init; }
}

internal sealed class OpenAiChatResponse
{
    public IReadOnlyList<OpenAiChoice>? Choices { get; set; }
}

internal sealed class OpenAiChoice
{
    public OpenAiChoiceMessage? Message { get; set; }

    public string? FinishReason { get; set; }
}

internal sealed class OpenAiChoiceMessage
{
    public string? Content { get; set; }

    public string? Refusal { get; set; }
}

internal sealed class OpenAiErrorResponse
{
    public OpenAiErrorDetails? Error { get; set; }
}

internal sealed class OpenAiErrorDetails
{
    public string? Message { get; set; }

    public string? Type { get; set; }

    public string? Code { get; set; }
}

internal sealed class OpenAiSummaryPayload
{
    public string? Title { get; set; }

    public string? ShortSummary { get; set; }

    public string? Summary { get; set; }

    public IReadOnlyList<OpenAiActionPayload>? ActionItems { get; set; }

    public IReadOnlyList<OpenAiActionPayload>? FollowUpItems { get; set; }

    public IReadOnlyList<OpenAiEntityPayload>? ImportantEntities { get; set; }
}

internal sealed class OpenAiActionPayload
{
    public string? Description { get; set; }

    public string? DueDate { get; set; }
}

internal sealed class OpenAiEntityPayload
{
    public string? Name { get; set; }

    public string? Type { get; set; }
}
