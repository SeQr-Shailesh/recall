# Architecture

SeQr Recall is a **modular monolith**. One ASP.NET Core 10 process, one SQL Server database, one React Native client (Android first). Provider boundaries are interfaces so STT, AI, file storage, OTP, and a future CRM can change without rewriting business logic.

## Goals

- Android ships first; iOS reuses the same APIs and mobile business modules.
- The backend is production-ready for Windows Server + IIS + SQL Server.
- Users speak mixed Indian languages; stored transcripts are English.
- Customer interactions are generic activities, not a full CRM.

## Solution layers

```text
SeQrRecall.Api            HTTP host, middleware, composition root
        │
        ├── SeQrRecall.Application    use cases, DTOs, contracts
        │         │
        │         └── SeQrRecall.Domain     entities and rules
        │
        └── SeQrRecall.Infrastructure     EF Core, providers, storage
                    │
                    └── SeQrRecall.Application
```

Rules:

- Domain has zero NuGet and zero project references.
- Application never references Infrastructure or Api.
- Controllers stay thin. They do not call STT, AI, or SQL directly.
- The mobile app never calls AI/STT providers.

These rules are enforced by unit tests in `tests/SeQrRecall.UnitTests/Architecture`.

## Processing pipeline

Notes and customer interactions share one orchestrator: `INoteProcessingService`.

```text
Mobile records audio
  → POST audio (request returns quickly)
  → status Uploaded
  → IBackgroundJobQueue
  → ProcessNoteAsync / ProcessCustomerInteractionAsync
       → load entity + audio
       → ISpeechToTextService.TranscribeAsync
       → English transcript stored
       → IAiSummaryService.SummarizeAsync
       → title, summaries, action items, follow-ups
       → Completed
```

HTTP requests must not wait for STT/AI. Phase 3 uses `InProcessBackgroundJobQueue` plus a hosted worker that is safe under IIS in-process hosting. `IBackgroundJobQueue` remains the seam for Hangfire, Azure Queue, SQS, or RabbitMQ later.

### Partial success (STT ok, AI failed)

The v1 status enum is:

`Draft`, `Uploading`, `Uploaded`, `Processing`, `Completed`, `Failed`

When transcription succeeds and summarization fails:

- Status is `Completed`
- `Transcript` is persisted
- Summary fields remain null
- `ProcessingError` describes the AI failure

The transcript is never discarded. A seventh status is not introduced in v1.

## Provider abstractions

| Contract | First implementation | Later |
| --- | --- | --- |
| `IOtpService` | `DummyOtpService` (Phase 2) | Msg91 / email provider |
| `ISpeechToTextService` | `MockSpeechToTextService` (Phase 3), `SarvamSpeechToTextService` (Phase 6) | Google, Azure, Deepgram, Whisper |
| `IAiSummaryService` | `MockAiSummaryService` (Phase 3), `OpenAiSummaryService` (Phase 7) | other LLMs |
| `IFileStorageService` | `LocalFileStorageService` (Phase 1) | S3 / Azure Blob |
| `IBackgroundJobQueue` | `InProcessBackgroundJobQueue` (Phase 3) | Hangfire / queues |

Provider selection is configuration (`Speech:Provider`, `AI:Provider`, `Storage:Provider`), not scattered `if` statements in controllers.

## CRM readiness

v1 is **not** a CRM. `Customers` and `CustomerInteractions` are a foundation:

- Interactions are generic activities (photo + audio + summary + action items).
- They are not hardcoded as sales visits.
- `ICrmIntegrationService` is **not** implemented. When CRM work starts, an integration service can publish activities outward.

Future CRM entities (Lead, Opportunity, Task, Follow-up) can reference `CustomerId` / `CustomerInteractionId` without rewriting Recall.

## Mobile architecture

Bare React Native + TypeScript. Shared (non-native) modules:

- `api/`, `services/`, `store/`, `types/`, `theme/`, `navigation/`, `screens/`

Android-only: microphone, camera, audio recording, Keystore-backed secure storage.

Tokens are never stored in plain AsyncStorage.

## API versioning

All production endpoints live under `/api/v1`. Breaking changes require `/api/v2`.

## Deployment shape

Single IIS site, `No Managed Code` app pool, SQL Server on the same or a nearby host, files on a disk **outside** the web root (`D:\SeQrRecallData` in production; `%LOCALAPPDATA%\SeQrRecallData` in development).

## Out of scope for the monolith

No microservices, Kubernetes, Redis, RabbitMQ, Kafka, CQRS frameworks, or extra databases unless a later requirement appears.
