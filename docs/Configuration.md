# Configuration

Secrets never belong in Git. Committed JSON files contain **placeholders only**.

## Files

| File | Purpose |
| --- | --- |
| `src/SeQrRecall.Api/appsettings.json` | Base placeholders |
| `appsettings.Development.json` | Local SQL + local storage path pattern |
| `appsettings.Staging.json` | Staging placeholders |
| `appsettings.Production.json` | Production placeholders (`D:\SeQrRecallData`) |
| `.env.example` | Environment variable names |
| `mobile/SeQrRecall.Mobile/.env.example` | Mobile API URLs |

Override with:

- `dotnet user-secrets` (Development)
- Environment variables (`Jwt__Secret`)
- IIS application settings

## Sections

### ConnectionStrings

`DefaultConnection` — SQL Server. Empty in Production JSON; set on the server.

### Jwt

| Key | Default | Notes |
| --- | --- | --- |
| Issuer | SeQrRecall | |
| Audience | SeQrRecall.Mobile | |
| Secret | empty | min 32 characters; from secrets store |
| AccessTokenMinutes | 30 | |
| RefreshTokenDays | 30 | |

Bound to `JwtOptions`. Startup fails if the secret is missing or shorter than 32 characters.

### Otp

| Key | Default |
| --- | --- |
| Provider | Dummy |

Bound to `OtpOptions`. `Dummy` registers `DummyOtpService`. Other providers are not implemented yet.

### Speech

| Key | Default | Notes |
| --- | --- | --- |
| Provider | Mock | `Mock` or `Sarvam` |
| ApiKey | empty | required when Provider is `Sarvam` |
| BaseUrl | `https://api.sarvam.ai` | |
| Model | `saaras:v3` | Saaras model id |

Bound to `SpeechOptions`. `Mock` registers `MockSpeechToTextService`. `Sarvam` registers `SarvamSpeechToTextService` and always requests `mode=translate`. Startup fails if `Speech:Provider` is `Sarvam` without `Speech:ApiKey`, or if the provider name is unimplemented.

### AI

| Key | Default | Notes |
| --- | --- | --- |
| Provider | Mock | Switch: `Mock`, `OpenAI`, `Gemini`, or `Qwen` |
| OpenAI:ApiKey / Model / BaseUrl | empty / `gpt-4o-mini` / OpenAI host | used when Provider is `OpenAI` |
| Gemini:ApiKey / Model / BaseUrl | empty / `gemini-3.5-flash` / Gemini host | used when Provider is `Gemini` |
| Qwen:ApiKey / Model / BaseUrl | empty / `qwen3-4b` / `http://127.0.0.1:8000/v1` | used when Provider is `Qwen`; API key optional for a local server |

Bound to `AiOptions`. `Mock` registers `MockAiSummaryService`. `OpenAI` registers `OpenAiSummaryService`. `Gemini` registers `GeminiSummaryService`. `Qwen` registers `QwenSummaryService`. Startup fails if OpenAI or Gemini is selected with no API key (`AI:{Provider}:ApiKey`, or flat `AI:ApiKey` as a fallback), or if the provider name is unimplemented. Qwen does not require an API key. To add another vendor later: new nested section + `RegisterAiProvider` branch.

### Storage

| Key | Development | Production |
| --- | --- | --- |
| Provider | Local | Local |
| RootPath | `%LOCALAPPDATA%\SeQrRecallData` | `D:\SeQrRecallData` |

This development PC has **no D: drive**. Phase 1 `LocalFileStorageService` expands environment variables and creates the tree at startup. The application never hard-codes `D:\`.

### Cors

`Cors:AllowedOrigins` — string array. In Development, an empty list allows any origin (Swagger / local tools). In Staging/Production, an empty list denies browser origins. `X-Correlation-ID` is exposed to browsers. Native mobile apps do not use CORS.

### Uploads

| Key | Default | Notes |
| --- | --- | --- |
| AudioMaxBytes | 26214400 (25 MB) | notes and interaction audio |
| PhotoMaxBytes | 5242880 (5 MB) | customer and interaction photos |

Bound to `UploadsOptions`. Each value must be between 1 and 104857600 (100 MB hard ceiling for Kestrel/IIS). Changing these does not require a rebuild.

### RateLimiting

| Key | Production / Staging | Development |
| --- | --- | --- |
| OtpSendPermitLimit | 5 | 300 |
| OtpVerifyPermitLimit | 10 | 300 |
| OtpWindowSeconds | 60 | 60 |
| UploadPermitLimit | 20 | 300 |
| UploadWindowSeconds | 60 | 60 |

Bound to `RateLimitOptions`. Sliding window per client IP, or per authenticated user id for uploads. Rejected requests return HTTP 429 with the v1 envelope. Development limits are high so local use and integration tests are not blocked.

### Serilog

Configured under `Serilog`. Development writes to the console and to `logs/seqr-recall-.log` (14-day retention). Production JSON does not enable a file sink until the server path is set. Do not log OTP values, JWT tokens, API keys, or audio contents.

## Environments

`Development`, `Staging`, `Production` via `ASPNETCORE_ENVIRONMENT`.

## Mobile

| Environment | Base URL |
| --- | --- |
| development | `http://10.0.2.2:5080` (emulator → host loopback) |
| staging | `https://staging.example.com` placeholder |
| production | `https://api.example.com` placeholder; HTTPS required |

Replace placeholders before a release. Do not scatter URLs in screens.

## What is not configured yet

Set `Speech:ApiKey` and `AI:ApiKey` via user secrets or environment variables. Production JSON lists `Speech:Provider = Sarvam` and `AI:Provider = OpenAI`; that host will not start until both API keys (and `Jwt:Secret`) are set.
