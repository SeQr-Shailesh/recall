# API

Base path: `/api/v1`

All responses use the envelope below. Pagination payloads sit in `data`.

## Envelope

Success:

```json
{
  "success": true,
  "data": {},
  "message": null
}
```

Error:

```json
{
  "success": false,
  "data": null,
  "message": "Unable to process request.",
  "errors": []
}
```

Internal exceptions are never returned. Correlation id is sent as `X-Correlation-ID`.

## Pagination

Query: `pageNumber` (default 1), `pageSize` (default 20, max 100), optional `search`.

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

## Authentication

Anonymous:

| Method | Path | Body |
| --- | --- | --- |
| POST | `/api/v1/auth/send-otp` | `{ "mobile": "+91...", "email": null }` or email |
| POST | `/api/v1/auth/verify-otp` | `{ "mobile": "...", "email": null, "otp": "123456", "deviceInfo": "..." }` |
| POST | `/api/v1/auth/refresh-token` | `{ "refreshToken": "...", "deviceInfo": "..." }` |

Authenticated:

| Method | Path | Notes |
| --- | --- | --- |
| POST | `/api/v1/auth/logout` | `{ "refreshToken": "..." }` revokes that token |
| GET | `/api/v1/users/me` | current user |
| PUT | `/api/v1/users/me` | `{ "fullName", "mobile", "email" }` |

`verify-otp` data:

```json
{
  "tokens": {
    "accessToken": "...",
    "refreshToken": "...",
    "accessTokenExpiresOn": "..."
  },
  "user": { "id": "...", "fullName": "", "isProfileComplete": false },
  "isNewUser": true
}
```

Phase 2 dummy OTP: any send appears successful; only `123456` validates. Implemented.

## Notes

All note routes require JWT. Ownership is enforced from claims.

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/v1/notes` | paged list; optional `search` on title, summary, transcript |
| POST | `/api/v1/notes` | creates `Draft` note, returns `{ "id", "processingStatus" }` |
| GET | `/api/v1/notes/{id}` | details (no audio bytes) |
| POST | `/api/v1/notes/{id}/audio` | multipart audio upload; queues processing |
| GET | `/api/v1/notes/{id}/status` | `{ "id", "processingStatus", "processingError", "completedOn" }` |
| GET | `/api/v1/notes/{id}/audio` | authorized stream |
| DELETE | `/api/v1/notes/{id}` | soft delete |

List items omit transcript and audio. Details include summaries, action items, transcript, and `hasAudio`. Audio upload is multipart field `file` (optional `durationSeconds`). Processing is asynchronous; poll `GET .../status`. STT is `ISpeechToTextService` (`Mock` in Development, `Sarvam` when configured). AI is `IAiSummaryService` (`Mock` in Development, `OpenAI` when configured).

## Leads

A lead is a note plus an optional photo. All lead routes require JWT and enforce ownership from claims.

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/v1/leads` | paged list; optional `search` on title, summary, transcript |
| POST | `/api/v1/leads` | creates `Draft` lead, returns `{ "id", "processingStatus" }` |
| GET | `/api/v1/leads/{id}` | details (no audio/photo bytes); includes `hasAudio`, `hasPhoto` |
| POST | `/api/v1/leads/{id}/audio` | multipart field `file` (optional `durationSeconds`); queues STT + AI |
| GET | `/api/v1/leads/{id}/status` | `{ "id", "processingStatus", "processingError", "completedOn" }` |
| GET | `/api/v1/leads/{id}/audio` | authorized stream |
| POST | `/api/v1/leads/{id}/photo` | multipart field `file` (JPEG, PNG, or WebP, max 5 MB); returns the updated details |
| GET | `/api/v1/leads/{id}/photo` | authorized image stream |
| DELETE | `/api/v1/leads/{id}` | soft delete |

List items carry `hasPhoto` so a client can render a thumbnail without fetching details. The photo is independent of the audio: it can be attached before the recording is uploaded or after processing completes, and re-uploading replaces the previous file. Processing matches notes: STT fail → `Failed`; AI fail after a transcript → `Completed` with `processingError`. User A requesting User B's lead, audio, or photo receives 404.

## Customers

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/v1/customers` | paged; `search` on name, company, mobile, email; newest first |
| POST | `/api/v1/customers` | `{ "name", "companyName", "mobile", "email" }`. `name` is required. |
| GET | `/api/v1/customers/{id}` | `{ "id", "name", "companyName", "mobile", "email", "hasPhoto", "createdOn", "isActive" }` |
| PUT | `/api/v1/customers/{id}` | same body as create |
| DELETE | `/api/v1/customers/{id}` | soft delete |
| POST | `/api/v1/customers/{id}/photo` | multipart field `file` (JPEG, PNG, or WebP, max 5 MB) |
| GET | `/api/v1/customers/{id}/photo` | authorized image stream |

List and detail JSON use `hasPhoto`. Stored file names and disk paths are never returned. Re-uploading a photo replaces the previous file. Create is JSON only; attach a photo with `POST .../photo` after create. User A requesting User B's customer or photo receives 404.


## Customer interactions

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/v1/customers/{customerId}/interactions` | timeline, newest first, paged; optional `search` on summary and transcript |
| POST | `/api/v1/customer-interactions` | `{ "customerId", "interactionDate" }` creates `Draft`. `interactionDate` defaults to now. |
| GET | `/api/v1/customer-interactions/{id}` | details (no audio/photo bytes); includes `hasAudio`, `hasPhoto` |
| POST | `/api/v1/customer-interactions/{id}/audio` | multipart field `file` (optional `durationSeconds`); queues STT + AI |
| GET | `/api/v1/customer-interactions/{id}/status` | `{ "id", "processingStatus", "processingError", "completedOn" }` |
| GET | `/api/v1/customer-interactions/{id}/audio` | authorized stream |
| POST | `/api/v1/customer-interactions/{id}/photo` | multipart field `file` (JPEG, PNG, or WebP, max 5 MB) |
| GET | `/api/v1/customer-interactions/{id}/photo` | authorized image stream |

List items omit transcript and audio. Details include summaries, action items, transcript, `hasAudio`, and `hasPhoto`. Stored file names are never returned. Processing matches notes: STT fail → `Failed`; AI fail after a transcript → `Completed` with `processingError`. User A requesting User B's customer or interaction receives 404.


## System

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/health` | anonymous; liveness (no database) |
| GET | `/health/ready` | anonymous; app + database. Body is `{ "status": "Healthy" }` with no connection details |
| GET | `/api/v1/system/version` | anonymous; `{ apiVersion, applicationVersion, environment }` — no secrets |

Swagger UI is available in Development and Staging at `/swagger`. Send `Authorization: Bearer {accessToken}` for protected routes.

## Authorization header

```text
Authorization: Bearer {accessToken}
```

## File uploads

- Audio uploads default to 25 MB (`Uploads:AudioMaxBytes`).
- Photo uploads default to 5 MB (`Uploads:PhotoMaxBytes`).
- Both are capped at 100 MB by Kestrel/IIS regardless of configuration.
- MIME and extension are validated.
- Server generates the stored file name.
- Physical paths are never returned.

## Stability

v1 response shapes are locked by contract tests for the envelope and pagination objects. Do not silently change them. Breaking changes require `/api/v2`.
