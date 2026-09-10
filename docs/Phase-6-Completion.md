# Phase 6 completion

## Completed

- Re-inspected `ISpeechToTextService`, `SpeechOptions`, notes processing, and Sarvam's published STT docs (Saaras v3).
- Added `SarvamSpeechToTextService` behind `Speech:Provider = Sarvam`. Development remains `Mock`. Production JSON already named `Sarvam`.
- REST: `POST /speech-to-text` with `model=saaras:v3` (configurable), `mode=translate`, `language_code=unknown`, header `api-subscription-key`.
- Batch: published job API when duration is greater than 30 seconds, or when REST rejects the file as too long.
- Failures (timeout, invalid JSON, empty transcript, 403, 429, outage) throw `ExternalProviderException`. The notes pipeline and mobile app are unchanged.
- Startup requires `Speech:ApiKey` when the provider is Sarvam.

## Files created

- [src/SeQrRecall.Infrastructure/Speech/SarvamSpeechToTextService.cs](../src/SeQrRecall.Infrastructure/Speech/SarvamSpeechToTextService.cs)
- [src/SeQrRecall.Infrastructure/Speech/SarvamSpeechContracts.cs](../src/SeQrRecall.Infrastructure/Speech/SarvamSpeechContracts.cs)
- [tests/SeQrRecall.UnitTests/Speech/SarvamSpeechToTextServiceTests.cs](../tests/SeQrRecall.UnitTests/Speech/SarvamSpeechToTextServiceTests.cs)
- [tests/SeQrRecall.UnitTests/Speech/SpeechProviderRegistrationTests.cs](../tests/SeQrRecall.UnitTests/Speech/SpeechProviderRegistrationTests.cs)

## Database changes

None.

## API changes

None. Notes still call `ISpeechToTextService` from the background worker.

## Mobile changes

None.

## Tests

```text
dotnet test SeQrRecall.sln      SUCCESS  88 passed, 0 failed
  UnitTests                     68 passed
  IntegrationTests              20 passed
```

## Known issues

1. Git is still not on PATH.
2. Production (and any host with `Speech:Provider = Sarvam`) will not start until `Speech:ApiKey` is set via user secrets, environment variables, or IIS.
3. AI summarization is still `MockAiSummaryService`. Real LLM work is Phase 7.
4. This phase did not call the live Sarvam API (no key in the repo). Unit tests use a scripted `HttpMessageHandler`.
5. Audio size cap remains 25 MB in code (Phase 10). Rate limiting remains Phase 10.
6. In-app audio playback of a saved note is not included.

## Next phase

**Phase 7 — LLM summarization**

Replace `MockAiSummaryService` with `OpenAiSummaryService` (or the configured provider) behind `AI:Provider`. Keep STT output as English input. Preserve the partial-success rule: STT ok + AI fail → `Completed` with transcript kept.

Before starting: re-inspect [docs/AI.md](AI.md) and `IAiSummaryService`.
