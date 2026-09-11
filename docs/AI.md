# AI summarization

All LLM traffic goes through `IAiSummaryService`. Controllers never call a vendor SDK.

## Contract

```csharp
Task<AiSummaryResult> SummarizeAsync(string englishTranscript, CancellationToken cancellationToken);
```

`AiSummaryResult` matches the required JSON:

```json
{
  "title": "...",
  "shortSummary": "...",
  "summary": "...",
  "actionItems": [],
  "followUpItems": [],
  "importantEntities": []
}
```

Never assume the model returned valid JSON. Parse, validate required fields, and fail gracefully.

## Rules

- Factual only. Do not invent commitments, deadlines, quantities, or orders.
- If the speaker said "might buy", the summary must not say "confirmed an order".
- Preserve names, companies, products, numbers, dates, locations.
- Do not translate proper nouns.
- Do not create a due date unless it was spoken.

## Failure handling

If STT succeeded and AI failed:

- Keep the English transcript.
- Set `ProcessingStatus` to `Completed`.
- Set `ProcessingError`.
- Leave title/summaries null.

The user must still open the note and read the transcript.

## Configuration

`AI:Provider` chooses the summarizer. Each vendor has its own nested section so keys stay configured together:

```json
"AI": {
  "Provider": "Qwen",
  "OpenAI": { "ApiKey": "", "BaseUrl": "https://api.openai.com/v1", "Model": "gpt-4o-mini" },
  "Gemini": { "ApiKey": "", "BaseUrl": "https://generativelanguage.googleapis.com/v1beta", "Model": "gemini-3.5-flash" },
  "Qwen": { "ApiKey": "", "BaseUrl": "http://127.0.0.1:8000/v1", "Model": "qwen3-4b", "TimeoutSeconds": 120 }
}
```

Change only `Provider` to switch (`Mock`, `OpenAI`, `Gemini`, or `Qwen`). Gemini and Qwen stay configured together; only the selected provider is called. Add a new nested object and a registration branch when another vendor is added.

| Provider | Implementation |
| --- | --- |
| `Mock` | `MockAiSummaryService` (base JSON default) |
| `OpenAI` | `OpenAiSummaryService` — Chat Completions `POST /v1/chat/completions` with `response_format.json_schema` (`strict: true`) |
| `Gemini` | `GeminiSummaryService` — `POST /v1beta/models/{model}:generateContent` with `responseMimeType=application/json` and `responseJsonSchema` |
| `Qwen` | `QwenSummaryService` — OpenAI-compatible `POST /v1/chat/completions` against a self-hosted Qwen3 server (`response_format.json_object`, model `qwen3-4b`) |

OpenAI default model is `gpt-4o-mini`. Gemini default model is `gemini-3.5-flash`. Qwen default model is `qwen3-4b`. OpenAI auth is `Authorization: Bearer`. Gemini auth is header `x-goog-api-key` (never a query string). Qwen sends `Authorization: Bearer` only when `AI:Qwen:ApiKey` (or flat `AI:ApiKey`) is set — a self-hosted server can leave the key empty. Startup fails if OpenAI or Gemini is selected and that provider's `ApiKey` is empty.

Point `AI:Qwen:BaseUrl` at the OpenAI-compatible endpoint of the process you deploy (vLLM, Ollama, llama.cpp, or SGLang), for example `http://127.0.0.1:8000/v1` on the same machine as the API. If that URL is empty or still an OpenAI/Gemini host, the client uses `http://127.0.0.1:8000/v1`. Serve Qwen over HTTPS if it is not on localhost.

When Provider is `Gemini` and the Gemini base URL is empty or still the OpenAI host, the client uses `https://generativelanguage.googleapis.com/v1beta`. Get a free key from [Google AI Studio](https://aistudio.google.com/apikey).

Invalid JSON, missing required fields, refusals/blocks, HTTP 401/429/503, and timeouts throw `ExternalProviderException`. The notes pipeline then keeps the transcript and marks the note `Completed` with `ProcessingError`.

## Language

Input to the LLM is already English (from STT + normalization). The model must not "re-translate" names into English equivalents that change identity.
