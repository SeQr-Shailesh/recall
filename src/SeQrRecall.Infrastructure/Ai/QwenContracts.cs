namespace SeQrRecall.Infrastructure.Ai;

/// <summary>
/// OpenAI-compatible chat-completions request used by self-hosted Qwen servers
/// (vLLM, Ollama, llama.cpp, SGLang). json_object is more widely supported than json_schema.
/// </summary>
internal sealed class QwenChatRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<OpenAiChatMessage> Messages { get; init; }

    public QwenResponseFormat ResponseFormat { get; init; } = new();

    public double Temperature { get; init; } = 0.2;

    public int MaxTokens { get; init; } = 2048;

    public QwenChatTemplateKwargs ChatTemplateKwargs { get; init; } = new();
}

internal sealed class QwenResponseFormat
{
    public string Type { get; init; } = "json_object";
}

internal sealed class QwenChatTemplateKwargs
{
    public bool EnableThinking { get; init; }
}
