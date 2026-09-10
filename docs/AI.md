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
  "Provider": "Gemini",
  "OpenAI": { "ApiKey": "", "BaseUrl": "https://api.openai.com/v1", "Model": "gpt-4o-mini" },
  "Gemini": { "ApiKey": "", "BaseUrl": "https://generativelanguage.googleapis.com/v1beta", "Model": "gemini-3.5-flash" }
}
```

Change only `Provider` to switch (`Mock`, `OpenAI`, or `Gemini`). Add a new nested object and a registration branch when another vendor is added.

| Provider | Implementation |
| --- | --- |
| `Mock` | `MockAiSummaryService` (base JSON default) |
| `OpenAI` | `OpenAiSummaryService` — Chat Completions `POST /v1/chat/completions` with `response_format.json_schema` (`strict: true`) |
| `Gemini` | `GeminiSummaryService` — `POST /v1beta/models/{model}:generateContent` with `responseMimeType=application/json` and `responseJsonSchema` |

OpenAI default model is `gpt-4o-mini`. Gemini default model is `gemini-3.5-flash`. OpenAI auth is `Authorization: Bearer`. Gemini auth is header `x-goog-api-key` (never a query string). Startup fails if the selected provider's `ApiKey` is empty (nested `AI:OpenAI:ApiKey` / `AI:Gemini:ApiKey`, or flat `AI:ApiKey` as a fallback).

When Provider is `Gemini` and the Gemini base URL is empty or still the OpenAI host, the client uses `https://generativelanguage.googleapis.com/v1beta`. Get a free key from [Google AI Studio](https://aistudio.google.com/apikey).

Invalid JSON, missing required fields, refusals/blocks, HTTP 401/429/503, and timeouts throw `ExternalProviderException`. The notes pipeline then keeps the transcript and marks the note `Completed` with `ProcessingError`.

## Language

Input to the LLM is already English (from STT + normalization). The model must not "re-translate" names into English equivalents that change identity.
