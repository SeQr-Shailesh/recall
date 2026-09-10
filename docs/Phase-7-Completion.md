# Phase 7 completion

## Completed

- Re-inspected `IAiSummaryService`, `AiOptions`, note processing (STT ok + AI fail → `Completed` with transcript), and OpenAI's published Chat Completions structured-output docs.
- Added `OpenAiSummaryService` behind `AI:Provider = OpenAI`. Development remains `Mock`. Production JSON is now `OpenAI`.
- Request: `POST /v1/chat/completions` with `Authorization: Bearer`, configurable `AI:Model` (default `gpt-4o-mini`), and `response_format` `json_schema` (`name: note_summary`, `strict: true`).
- System prompt encodes the product rules: factual only, do not invent commitments or due dates, do not re-translate proper nouns.
- Invalid JSON, missing required fields, refusals, 401, 429, timeouts, and outages throw `ExternalProviderException`. The notes pipeline and mobile app are unchanged.
- Startup requires `AI:ApiKey` when the provider is OpenAI.

## Files created

- [src/SeQrRecall.Infrastructure/Ai/OpenAiSummaryService.cs](../src/SeQrRecall.Infrastructure/Ai/OpenAiSummaryService.cs)
- [src/SeQrRecall.Infrastructure/Ai/OpenAiChatContracts.cs](../src/SeQrRecall.Infrastructure/Ai/OpenAiChatContracts.cs)
- [tests/SeQrRecall.UnitTests/Ai/OpenAiSummaryServiceTests.cs](../tests/SeQrRecall.UnitTests/Ai/OpenAiSummaryServiceTests.cs)
- [tests/SeQrRecall.UnitTests/Ai/AiProviderRegistrationTests.cs](../tests/SeQrRecall.UnitTests/Ai/AiProviderRegistrationTests.cs)

## Database changes

None.

## API changes

None. Notes still call `IAiSummaryService` from the background worker.

## Mobile changes

None.

## Tests

```text
dotnet test SeQrRecall.sln      SUCCESS  101 passed, 0 failed
  UnitTests                     81 passed
  IntegrationTests              20 passed
```

## Known issues

1. Git is still not on PATH.
2. Production (and any host with `Speech:Provider = Sarvam` and `AI:Provider = OpenAI`) will not start until `Speech:ApiKey`, `AI:ApiKey`, and `Jwt:Secret` are set.
3. This phase did not call the live OpenAI API (no key in the repo). Unit tests use a scripted `HttpMessageHandler`.
4. Customer APIs and Customer Recall screens are not implemented yet (Phase 8).
5. Audio size cap remains 25 MB in code (Phase 10). Rate limiting remains Phase 10.
6. In-app audio playback of a saved note is not included.

## Next phase

**Phase 8 — Customers (backend + Android list/create)**

Implement owned customer CRUD under `/api/v1/customers` (JWT claims only). Finalize photo upload if it is part of create/update. Add Android customer list/search/create without building the interaction timeline yet (Phase 9).

Before starting: re-inspect [docs/API.md](API.md) customer contracts, `Customer` entity, and the mobile navigation scaffold.
