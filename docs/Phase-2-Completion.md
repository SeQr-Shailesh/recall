# Phase 2 completion

## Completed

- Re-inspected Phase 1 host, `InitialCreate`, and auth contracts. Notes and customers were not added.
- Implemented passwordless login: send OTP, verify OTP, JWT access tokens, refresh-token rotation, logout, and profile get/update.
- `IOtpService` is implemented by `DummyOtpService`. Controllers never compare OTP strings. Only `123456` validates. Send always appears successful. The dummy code is logged in Development only.
- `IJwtTokenService` issues HMAC-SHA256 access tokens (`sub` + `nameid`) and SHA-256 hashed refresh tokens.
- Reuse of a revoked refresh token fails and revokes remaining tokens for that user.
- `GET/PUT /api/v1/users/me` are authorized from JWT claims. Client-supplied user ids are ignored.
- JWT bearer is wired in the pipeline. Unauthenticated `/users/me` returns the v1 401 envelope.

## Files created

- [src/SeQrRecall.Application/Services/AuthenticationService.cs](../src/SeQrRecall.Application/Services/AuthenticationService.cs)
- [src/SeQrRecall.Application/Abstractions/Authentication/IAuthenticationService.cs](../src/SeQrRecall.Application/Abstractions/Authentication/IAuthenticationService.cs)
- [src/SeQrRecall.Infrastructure/Authentication/DummyOtpService.cs](../src/SeQrRecall.Infrastructure/Authentication/DummyOtpService.cs)
- [src/SeQrRecall.Infrastructure/Security/JwtTokenService.cs](../src/SeQrRecall.Infrastructure/Security/JwtTokenService.cs)
- [src/SeQrRecall.Api/Controllers/AuthController.cs](../src/SeQrRecall.Api/Controllers/AuthController.cs)
- [src/SeQrRecall.Api/Controllers/UsersController.cs](../src/SeQrRecall.Api/Controllers/UsersController.cs)
- Unit tests: `DummyOtpServiceTests`, `JwtTokenServiceTests`
- Integration tests: `AuthenticationTests`

## Database changes

None. Users and RefreshTokens from `InitialCreate` were sufficient.

## API changes

| Method | Path | Auth |
| --- | --- | --- |
| POST | `/api/v1/auth/send-otp` | anonymous |
| POST | `/api/v1/auth/verify-otp` | anonymous |
| POST | `/api/v1/auth/refresh-token` | anonymous |
| POST | `/api/v1/auth/logout` | JWT |
| GET | `/api/v1/users/me` | JWT |
| PUT | `/api/v1/users/me` | JWT |

## Mobile changes

None. Android login screens remain Phase 4.

## Tests

```text
dotnet build SeQrRecall.sln     SUCCESS  0 warnings, 0 errors
dotnet test SeQrRecall.sln      SUCCESS  48 passed, 0 failed
  UnitTests                     33 passed
  IntegrationTests              15 passed
```

Android compile was not run (no JDK/Android SDK).

## Known issues

1. Git is still not on PATH.
2. No Android toolchain.
3. Development `Jwt:Secret` is a placeholder string (32+ characters). Replace via user secrets before any shared environment.
4. `DummyOtpService` must not be used in Production once a real SMS/email provider exists. `Otp:Provider` is the swap point.
5. OTP send/verify are not rate-limited yet (Phase 10).
6. Profile photo upload is not an endpoint yet (optional field; camera/files land with later mobile work).
7. Integration tests share the Development `SeQrRecall` database.

## Next phase

**Phase 3 — Notes backend**

Notes entity APIs, audio upload, local file storage (already present), processing status, background processing infrastructure, pagination, and search. Use mock STT/AI providers. Do not integrate Sarvam or an LLM yet.

Before starting: re-inspect this tree, auth contracts, and [docs/API.md](API.md) notes section.
