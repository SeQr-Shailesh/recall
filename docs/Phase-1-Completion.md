# Phase 1 completion

## Completed

- Re-inspected Phase 0 code, docs, and API contracts. No auth, notes, or customer endpoints were added.
- Implemented EF Core SQL Server `ApplicationDbContext` with entity configurations, indexes, audit timestamps, and soft-delete query filters.
- Created and applied migration `InitialCreate` (`20260819162552_InitialCreate`) to local SQL Server database `SeQrRecall`.
- Implemented `LocalFileStorageService` (`IFileStorageService`) with generated file names and path-traversal protection.
- Implemented `CurrentUserService` from HTTP claims (no JWT middleware yet; always unauthenticated until Phase 2).
- Wired Serilog, global exception handling, correlation id, security headers, CORS, forwarded headers, API versioning, Swagger (Development/Staging), health checks, and `GET /api/v1/system/version`.
- Fail-fast if `ConnectionStrings:DefaultConnection` is missing. Production does **not** auto-apply migrations.

Authentication (OTP, JWT, refresh tokens) is **not** implemented. That is Phase 2.

## Files created

### Persistence

- [src/SeQrRecall.Infrastructure/Persistence/ApplicationDbContext.cs](../src/SeQrRecall.Infrastructure/Persistence/ApplicationDbContext.cs)
- [src/SeQrRecall.Infrastructure/Persistence/ApplicationDbContextFactory.cs](../src/SeQrRecall.Infrastructure/Persistence/ApplicationDbContextFactory.cs)
- Entity configurations under `Persistence/Configurations/`
- [src/SeQrRecall.Infrastructure/Persistence/Migrations/20260819162552_InitialCreate.cs](../src/SeQrRecall.Infrastructure/Persistence/Migrations/20260819162552_InitialCreate.cs)

### Infrastructure

- [src/SeQrRecall.Infrastructure/Storage/LocalFileStorageService.cs](../src/SeQrRecall.Infrastructure/Storage/LocalFileStorageService.cs)
- [src/SeQrRecall.Infrastructure/Security/CurrentUserService.cs](../src/SeQrRecall.Infrastructure/Security/CurrentUserService.cs)

### API

- Middleware: correlation id, exception handling, security headers
- [src/SeQrRecall.Api/Controllers/SystemController.cs](../src/SeQrRecall.Api/Controllers/SystemController.cs)
- [src/SeQrRecall.Api/OpenApi/ConfigureSwaggerOptions.cs](../src/SeQrRecall.Api/OpenApi/ConfigureSwaggerOptions.cs)
- Pipeline extensions

### Tooling

- [.config/dotnet-tools.json](../.config/dotnet-tools.json) — `dotnet-ef` 10.0.11

## Database changes

Migration **InitialCreate** creates:

- Users (filtered unique indexes on Mobile, Email)
- RefreshTokens
- Notes, NoteActionItems
- Customers, CustomerInteractions, CustomerInteractionActionItems
- AuditLogs

Soft-delete filters apply to Notes, Customers, and CustomerInteractions. ProcessingStatus and ActionItemKind are stored as strings.

Applied locally with:

```powershell
dotnet tool restore
dotnet ef database update --project src/SeQrRecall.Infrastructure --startup-project src/SeQrRecall.Api
```

## API changes

| Method | Path | Status |
| --- | --- | --- |
| GET | `/health` | implemented (liveness, no DB) |
| GET | `/health/ready` | implemented (EF DbContext check) |
| GET | `/api/v1/system/version` | implemented |
| GET | `/swagger` | Development and Staging |

Error responses use the v1 envelope. `X-Correlation-ID` is always returned, including after exceptions.

## Mobile changes

None.

## Tests

```text
dotnet build SeQrRecall.sln     SUCCESS  0 warnings, 0 errors
dotnet test SeQrRecall.sln      SUCCESS  30 passed, 0 failed
  UnitTests                     23 passed
  IntegrationTests               7 passed
```

Android compile was not run (no JDK/Android SDK). Unchanged from Phase 0.

## Known issues

1. Git is still not on PATH. Nothing was committed.
2. No JDK / Android SDK. Android verification remains Phase 4.
3. No `D:` drive. Development storage is `%LOCALAPPDATA%\SeQrRecallData`.
4. Integration tests use the Development database `SeQrRecall`, not an isolated test database.
5. `IBackgroundJobQueue`, OTP, JWT, STT, and AI remain unimplemented (later phases).
6. Swagger shows a Bearer scheme but no endpoints require JWT yet.
7. `AssumeDefaultVersionWhenUnspecified` is false (Asp.Versioning AV0016). Clients must use `/api/v1/...`.

## Next phase

**Phase 2 — Authentication**

Implement User + OTP (`DummyOtpService`, accept `123456` only), JWT access tokens, refresh tokens, logout, and `GET/PUT /api/v1/users/me`. Do not add notes or customers yet.

Before starting: re-inspect this tree, `InitialCreate`, and the auth contract in [API.md](API.md) and [Authentication.md](Authentication.md).
