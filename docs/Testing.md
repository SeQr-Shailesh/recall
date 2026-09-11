# Testing

Tests must assert real behavior. Do not add empty tests for coverage.

## Phase 0

| Test | What it proves |
| --- | --- |
| `LayerDependencyTests` | Domain has no SeQrRecall refs; Application does not reference Infrastructure/Api |
| `ApiResponseContractTests` | Success/error JSON matches the v1 envelope |
| `PagedResultContractTests` | Pagination JSON and page-size clamp |
| `ProcessingStatusTests` | Exactly the six v1 statuses |
| `HostAssemblyTests` | API host assembly loads; option section names match |

## Phase 1

| Test | What it proves |
| --- | --- |
| `ExceptionHandlingMiddlewareTests` | 404/400/500 envelopes; internals are not returned |
| `CorrelationIdMiddlewareTests` | Generates or reuses `X-Correlation-ID` |
| `LocalFileStorageServiceTests` | Generated file names, path-traversal rejection, round-trip |
| `SoftDeleteQueryFilterTests` | Deleted notes/customers are hidden |
| `ApplicationModelTests` | Filtered unique indexes and query filters exist on the EF model |
| `FoundationEndpointTests` | `/health`, `/health/ready`, `/api/v1/system/version`, Swagger, security headers, Development CORS |

```powershell
dotnet test SeQrRecall.sln
```

Phase 1 integration tests use the Development SQL Server database (`SeQrRecall`). Apply `InitialCreate` first.

## Phase 2

| Test | What it proves |
| --- | --- |
| `DummyOtpServiceTests` | Send succeeds; only `123456` validates |
| `JwtTokenServiceTests` | Access token claims, refresh hash, secret length |
| `AuthenticationTests` | Register → OTP 123456 → JWT → profile → refresh rotation → logout; invalid OTP/JWT/401 |

Integration tests share one `ApiWebApplicationFactory` collection so Serilog is not frozen twice.

## Phase 3

| Test | What it proves |
| --- | --- |
| `AudioUploadRulesTests` | MIME allow-list, extension fallback, 25 MB cap |
| `MockSpeechToTextServiceTests` | Labeled mock transcript |
| `MockAiSummaryServiceTests` | Title and action items from the mock transcript |
| `NoteProcessingServiceTests` | STT fail → Failed; AI fail → Completed + transcript; success → summaries |
| `NotesServiceTests` | Draft create, ownership 404, upload validation, queue, soft delete |
| `InProcessBackgroundJobQueueTests` | Note jobs are readable |
| `NotesTests` | Auth required; upload → process → search; IDOR 404; invalid MIME; pagination |

## Phase 4

| Test | What it proves |
| --- | --- |
| `npx tsc --noEmit` | Auth screens, API client, keychain storage, and navigation typecheck |

Gradle / emulator runs remain blocked until JDK 17 and the Android SDK are installed.

## Phase 5

| Test | What it proves |
| --- | --- |
| `npx tsc --noEmit` | Notes list/search, record, upload, status poll, details, and delete typecheck |

## Phase 6

| Test | What it proves |
| --- | --- |
| `SarvamSpeechToTextServiceTests` | REST `mode=translate` maps English + language; original file name is not sent; 403/429/invalid JSON/empty transcript/timeout → `ExternalProviderException`; duration > 30s uses Batch; REST duration rejection falls back to Batch |
| `SpeechProviderRegistrationTests` | `Mock` and `Sarvam` register the matching service; missing Sarvam API key and unknown providers fail at startup |

## Phase 7

| Test | What it proves |
| --- | --- |
| `OpenAiSummaryServiceTests` | Structured JSON maps to title/summary/action items; ISO due dates parse and relative words do not; 401/429/invalid JSON/refusal/timeout/missing title → `ExternalProviderException`; API key is not in the body or exception |
| `AiProviderRegistrationTests` | `Mock`, `OpenAI`, `Gemini`, and `Qwen` register the matching service; missing OpenAI/Gemini API keys and unknown providers fail at startup; Qwen starts without an API key |
| `QwenSummaryServiceTests` | Structured JSON maps to title/summary/action items; thinking tags are stripped; 401/429/invalid JSON/refusal/timeout/missing title → `ExternalProviderException`; bearer token is sent only when a key is configured |

## Phase 8

| Test | What it proves |
| --- | --- |
| `PhotoUploadRulesTests` | JPEG/PNG/WebP allow-list, extension fallback, 5 MB cap |
| `CustomersServiceTests` | Owned create/update, validation, search, soft delete, IDOR 404, photo replace, missing photo 404 |
| `CustomersTests` | Auth required; CRUD + search; photo stream; IDOR 404; invalid MIME; blank name; pagination |
| `npx tsc --noEmit` | Notes/Customers tabs, list/search/create/edit/details, optional photo typecheck |

## Phase 9

| Test | What it proves |
| --- | --- |
| `NoteProcessingServiceTests` (interaction cases) | STT fail → Failed; AI fail → Completed + transcript; success → summaries and action items |
| `CustomerInteractionsServiceTests` | Owned create, customer IDOR, interaction IDOR, audio validation/queue, photo replace |
| `InProcessBackgroundJobQueueTests` | Note and interaction jobs are readable |
| `CustomerInteractionsTests` | Auth required; create → photo → audio → process → timeline search; IDOR 404; invalid MIME |
| `npx tsc --noEmit` | Customer timeline, record conversation, details, optional photo typecheck |

## Phase 10

| Test | What it proves |
| --- | --- |
| `UploadsOptionsTests` | Defaults are 25 MB audio / 5 MB photo; multipart limit never exceeds 100 MB |
| `NotesServiceTests` configured cap | A lowered `Uploads:AudioMaxBytes` is enforced |
| `RateLimitRejectionWriterTests` | Partition key prefers user id then IP; invalid limits are clamped |
| `HostAssemblyTests` | `Uploads` and `RateLimiting` section names match |
| `FoundationEndpointTests` | CSP, Permissions-Policy, Cache-Control no-store, Development CORS expose correlation id |

## Phase 11

| Test | What it proves |
| --- | --- |
| `CurrentUserServiceTests` | `UserId` comes from JWT `NameIdentifier` / `sub`; a client `X-User-Id` header is ignored |
| `OwnershipIsolationTests` | User B receives 404 for User A's notes, customers, interactions, audio, and photos; search lists do not leak; extra `userId` JSON is ignored; files are not static |
| `AndroidFunctionalFlowTests` | Login, profile, note record/upload/poll/search, customer photo, conversation timeline match the Android API paths |
| `npx tsc --noEmit` | Mobile TypeScript still typechecks |
| `gradlew assembleDebug` | Android debug APK builds (`com.seqr.recall`) |

## Leads

| Test | What it proves |
| --- | --- |
| `LeadsServiceTests` | Owned create, IDOR 404, audio validation/queue, photo before audio, photo replace deletes the old file, `hasPhoto` on list items, soft delete hides the lead |
| `LeadsTests` | Auth required; create → photo → audio → process → search → streams → delete; photo attaches after processing completes; IDOR 404 on every route; invalid photo MIME |
| `gradlew assembleRelease` | Standalone APK builds with the Hermes bundle embedded (`com.seqr.recall`) |

`ApiWebApplicationFactory` pins `Speech:Provider` and `AI:Provider` to `Mock`. Without that the test host inherits the Development configuration, calls the live Sarvam and OpenAI endpoints with synthetic audio, and every processing assertion fails with `Failed`.

## Later phases

v1 acceptance is complete. Remaining product work (iOS, real OTP, Hangfire, in-app audio playback) is optional and is not started here.

## Integration tests and SQL

Prefer a dedicated test database or Testcontainers if available. Do not point tests at production.

## Mobile

Phase 0 typecheck:

```powershell
cd mobile/SeQrRecall.Mobile
npx tsc --noEmit
```
