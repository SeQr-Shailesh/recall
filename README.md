# SeQr Recall

**Speak it. Recall it.**

SeQr Recall is a production AI voice-note and customer-interaction platform. Users speak naturally in English, Indian English, Hindi, Marathi, Gujarati, Hinglish, or mixed languages. The system converts speech to English, summarizes it, extracts action items, and remembers it.

This is a modular monolith: ASP.NET Core 10 + SQL Server + React Native (Android first). iOS, CRM integration, and additional AI/STT providers can be added later without rewriting the backend.

## Product

- Passwordless login (mobile or email + OTP)
- Voice notes with asynchronous STT and AI summary
- Customer Recall: photo + conversation + timeline
- Server-side search and pagination
- Deployable on Windows Server, IIS, and SQL Server

## Architecture

Clean architecture, four backend projects:

| Project | Responsibility |
| --- | --- |
| `SeQrRecall.Domain` | Entities and domain rules |
| `SeQrRecall.Application` | Use cases, DTOs, provider contracts |
| `SeQrRecall.Infrastructure` | EF Core, SQL Server, STT, AI, storage, auth implementations |
| `SeQrRecall.Api` | Controllers, middleware, Swagger, composition root |

Mobile lives in `mobile/SeQrRecall.Mobile` (bare React Native, TypeScript). Shared business logic stays on the backend so iOS can be added later.

See [docs/Architecture.md](docs/Architecture.md).

## Repository structure

```text
SeQrRecall.sln
src/
  SeQrRecall.Api/
  SeQrRecall.Application/
  SeQrRecall.Domain/
  SeQrRecall.Infrastructure/
tests/
  SeQrRecall.UnitTests/
  SeQrRecall.IntegrationTests/
mobile/
  SeQrRecall.Mobile/
docs/
```

## Requirements

- .NET SDK 10.0
- SQL Server (local default instance or LocalDB)
- Node.js 20+ (24 is fine)
- Android Studio, JDK 17, and Android SDK (from Phase 4, to run the app)

## Local setup

1. Clone this repository.
2. Copy [.env.example](.env.example) values into user secrets or environment variables. Do not put real secrets in source files.
3. Restore and build:

```powershell
dotnet restore
dotnet build SeQrRecall.sln
```

## SQL Server setup

Create an empty database named `SeQrRecall` on the local instance. Example:

```sql
CREATE DATABASE SeQrRecall;
```

Phase 0 defined the schema only. Phase 1 added `InitialCreate`. Restore the local EF tool, then apply migrations (creates `SeQrRecall` if the login can create databases):

```powershell
dotnet tool restore
dotnet ef database update --project src/SeQrRecall.Infrastructure --startup-project src/SeQrRecall.Api
```

Development connection string (Windows auth):

```text
Server=localhost;Database=SeQrRecall;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

## Backend startup

```powershell
dotnet run --project src/SeQrRecall.Api --launch-profile https
```

- HTTP: `http://localhost:5080`
- HTTPS: `https://localhost:7080`
- Swagger (Development): `https://localhost:7080/swagger`
- Health: `GET /health` and `GET /health/ready`
- Version: `GET /api/v1/system/version`
- Auth: `POST /api/v1/auth/send-otp`, `POST /api/v1/auth/verify-otp` (OTP `123456`)
- Notes: `GET/POST /api/v1/notes`, audio upload, status, authorized audio stream
- Leads: `GET/POST /api/v1/leads`, audio upload, status, authorized audio stream, optional photo upload/stream
- Customers: `GET/POST /api/v1/customers`, update, delete, authorized photo upload/stream
- Customer interactions: timeline, create, audio upload/status/stream, optional photo

Do not auto-apply migrations on Production startup.

## Android startup

Native `android/` exists (`com.seqr.recall`). Login, voice notes, customers, and customer conversations are implemented. JDK 17 and the Android SDK are installed; use a new terminal so `JAVA_HOME` / `ANDROID_HOME` are on PATH.

```powershell
cd mobile/SeQrRecall.Mobile
npm install
npx tsc --noEmit
npx react-native run-android --active-arch-only   # requires JDK 17 + Android SDK
```

See [docs/AndroidSetup.md](docs/AndroidSetup.md).

## Configuration

Strongly typed options: `Jwt`, `Speech`, `AI`, `Storage`, `Otp`, `Uploads`, `RateLimiting`. Placeholders only in committed `appsettings.*.json`. Real values come from environment variables, user secrets, or IIS.

See [docs/Configuration.md](docs/Configuration.md).

## STT configuration

`Speech:Provider = Mock` uses `MockSpeechToTextService` (Development default). `Speech:Provider = Sarvam` uses `SarvamSpeechToTextService` (Production JSON). Set `Speech:ApiKey` outside source control. See [docs/SpeechToText.md](docs/SpeechToText.md).

## AI configuration

`AI:Provider = Mock` uses `MockAiSummaryService` (Development default). `AI:Provider = OpenAI` uses `OpenAiSummaryService` (Production JSON). Set `AI:ApiKey` outside source control. See [docs/AI.md](docs/AI.md).

## Running tests

```powershell
dotnet test SeQrRecall.sln
```

## Publishing the backend

```powershell
dotnet publish src/SeQrRecall.Api/SeQrRecall.Api.csproj -c Release -o .\publish
```

## IIS deployment

See [docs/Deployment.md](docs/Deployment.md) for the full Windows Server checklist.

## Documentation

- [Architecture](docs/Architecture.md)
- [Database](docs/Database.md)
- [API](docs/API.md)
- [Authentication](docs/Authentication.md)
- [Android setup](docs/AndroidSetup.md)
- [Deployment](docs/Deployment.md)
- [Configuration](docs/Configuration.md)
- [AI](docs/AI.md)
- [Speech-to-text](docs/SpeechToText.md)
- [Security](docs/Security.md)
- [Testing](docs/Testing.md)
- [Troubleshooting](docs/Troubleshooting.md)
