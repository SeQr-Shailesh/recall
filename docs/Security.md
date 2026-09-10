# Security

## Authentication and authorization

- JWT access tokens + hashed refresh tokens.
- `ICurrentUserService` is the only source of `UserId`.
- Every note, customer, interaction, audio, and photo query is filtered by that user.
- User A must not read User B's data. Covered by `OwnershipIsolationTests` (Phase 11): 404 on the other user's notes, customers, interactions, audio, and photos; lists/search do not leak; client-supplied `userId` JSON is ignored.

## Files

- Validate MIME type and extension.
- Cap file size.
- Generate server-side file names. Never use the client file name on disk.
- Block path traversal.
- Store outside the web root.
- Stream through authorized endpoints. Do not expose physical paths.

## HTTP

- Production HTTPS only. HSTS (`max-age=31536000`; includeSubDomains) is enabled outside Development.
- CORS allow-list. Development with an empty list allows any origin. Staging/Production with an empty list denies browser origins. `X-Correlation-ID` is exposed.
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Content-Security-Policy`, `Cache-Control: no-store` on `/api`.
- Rate limits on OTP send/verify and file uploads (configurable). HTTP 429 uses the v1 envelope.
- `X-Correlation-ID` on every response.

## Data

- EF Core parameterized queries; no interpolated SQL.
- Soft delete for notes and customers.
- Secrets from configuration/environment, never source control.
- Do not log OTP in Production, JWT tokens, API keys, or audio contents.

## Dummy OTP

`123456` is an intentional development back door. It must be confined to `DummyOtpService` and disabled when a real provider is registered. Do not ship Production with `DummyOtpService` unless a documented exception exists.

## Threats in scope for v1 tests

- Invalid / expired JWT
- Invalid / missing OTP
- IDOR on note, customer, interaction, audio, photo
- Oversized or unexpected file types
