# Phase 3 completion

## Completed

- Re-inspected auth, `IFileStorageService`, note DTOs, and the processing contracts. Customer APIs were not added.
- Implemented note create, paged list with search (title, short/full summary, transcript), details, status, multipart audio upload, authorized audio stream, and soft delete.
- Ownership is taken from JWT claims only. Another user's note id returns 404 (no existence leak).
- Upload validates MIME/extension, rejects files over 25 MB, stores a generated name via `LocalFileStorageService`, sets `Uploaded`, and queues work. HTTP does not wait for STT/AI.
- `InProcessBackgroundJobQueue` plus `BackgroundJobWorker` process notes under the IIS in-process host.
- `INoteProcessingService` runs STT then AI. STT failure → `Failed`. STT success + AI failure → `Completed` with transcript kept, summaries null, and `ProcessingError` set.
- Labeled mocks: `MockSpeechToTextService` (`Speech:Provider = Mock`) and `MockAiSummaryService` (`AI:Provider = Mock`). Unimplemented providers fail at startup.

## Files created

- [src/SeQrRecall.Application/Services/NotesService.cs](../src/SeQrRecall.Application/Services/NotesService.cs)
- [src/SeQrRecall.Application/Services/NoteProcessingService.cs](../src/SeQrRecall.Application/Services/NoteProcessingService.cs)
- [src/SeQrRecall.Application/Abstractions/Notes/INotesService.cs](../src/SeQrRecall.Application/Abstractions/Notes/INotesService.cs)
- [src/SeQrRecall.Application/Notes/AudioUploadRules.cs](../src/SeQrRecall.Application/Notes/AudioUploadRules.cs)
- [src/SeQrRecall.Application/Mapping/NoteMapping.cs](../src/SeQrRecall.Application/Mapping/NoteMapping.cs)
- [src/SeQrRecall.Infrastructure/Speech/MockSpeechToTextService.cs](../src/SeQrRecall.Infrastructure/Speech/MockSpeechToTextService.cs)
- [src/SeQrRecall.Infrastructure/Ai/MockAiSummaryService.cs](../src/SeQrRecall.Infrastructure/Ai/MockAiSummaryService.cs)
- [src/SeQrRecall.Infrastructure/Processing/InProcessBackgroundJobQueue.cs](../src/SeQrRecall.Infrastructure/Processing/InProcessBackgroundJobQueue.cs)
- [src/SeQrRecall.Infrastructure/Processing/BackgroundJobWorker.cs](../src/SeQrRecall.Infrastructure/Processing/BackgroundJobWorker.cs)
- [src/SeQrRecall.Api/Controllers/NotesController.cs](../src/SeQrRecall.Api/Controllers/NotesController.cs)
- Unit tests: `AudioUploadRulesTests`, `MockSpeechToTextServiceTests`, `MockAiSummaryServiceTests`, `NoteProcessingServiceTests`, `NotesServiceTests`, `InProcessBackgroundJobQueueTests`
- Integration tests: `NotesTests`

## Database changes

None. Notes and NoteActionItems from `InitialCreate` were sufficient.

## API changes

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/v1/notes` | JWT; paged; optional `search` |
| POST | `/api/v1/notes` | JWT; creates `Draft` |
| GET | `/api/v1/notes/{id}` | JWT |
| POST | `/api/v1/notes/{id}/audio` | JWT; multipart `file` |
| GET | `/api/v1/notes/{id}/status` | JWT |
| GET | `/api/v1/notes/{id}/audio` | JWT; raw stream |
| DELETE | `/api/v1/notes/{id}` | JWT; soft delete |

## Mobile changes

None. Android recording and upload remain Phase 5.

## Tests

```text
dotnet build SeQrRecall.sln     SUCCESS  0 warnings, 0 errors
dotnet test SeQrRecall.sln      SUCCESS  74 passed, 0 failed
  UnitTests                     54 passed
  IntegrationTests              20 passed
```

Android compile was not run (no JDK/Android SDK).

## Known issues

1. Git is still not on PATH.
2. No Android toolchain.
3. `MockSpeechToTextService` / `MockAiSummaryService` are not production transcription or summarization. Switch to Sarvam (Phase 6) and an LLM (Phase 7) before treating results as real.
4. Production `appsettings.Production.json` still has `Speech:Provider = Sarvam`. That host will not start until Phase 6 or until the value is explicitly `Mock`.
5. Audio size cap is a code constant (25 MB), not configuration (Phase 10).
6. Rate limiting on uploads is still deferred to Phase 10.
7. Customer interaction processing is queued in the abstraction but not implemented (Phase 9).
8. Integration tests share the Development `SeQrRecall` database.
9. In-process jobs are lost if the IIS worker process recycles before they run.

## Next phase

**Phase 4 — Android authentication**

Bare React Native Android project, login screens, OTP `123456`, JWT storage (not plain AsyncStorage), and calls to the Phase 2 auth APIs.

Before starting: re-inspect this tree, [docs/AndroidSetup.md](AndroidSetup.md), and the mobile TypeScript scaffold.
