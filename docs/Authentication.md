# Authentication

Passwordless. Users sign in with a mobile number or email plus an OTP. There is no password.

## Flow

1. Client posts `send-otp` with either `mobile` or `email`.
2. `IOtpService.SendOtpAsync` delivers (or, in v1 development, pretends to deliver) a code.
3. Client posts `verify-otp` with the same destination and the code.
4. `IOtpService.ValidateOtpAsync` decides validity. **Controllers do not compare OTP strings.**
5. If the user does not exist, the API creates the user (JWT `UserId` is the new id).
6. API issues a short-lived JWT access token and a refresh token (raw token once; hash stored).
7. If `isNewUser` is true or `isProfileComplete` is false, the Android app shows profile setup (`PUT /api/v1/users/me`).
8. Subsequent requests send `Authorization: Bearer`.
9. `POST /api/v1/auth/refresh-token` rotates the refresh token.
10. Logout revokes the presented refresh token.

## Dummy OTP (v1 only)

`DummyOtpService` is registered when `Otp:Provider = Dummy` (the only v1 implementation):

- `SendOtpAsync` returns success and logs the generated code **in Development only**. Production logging must never write OTP values.
- `ValidateOtpAsync` accepts **only** `123456`. Any other code fails.

The UI still behaves as if an SMS/email was sent. Replacing `DummyOtpService` with `Msg91OtpService` (or email) does not change mobile screens or controller signatures.

## JWT

Configured via `Jwt` options: Issuer, Audience, Secret, AccessTokenMinutes (30), RefreshTokenDays (30).

Claims must include a stable user id (`sub` or `nameid`). `ICurrentUserService` reads that claim. Client-supplied `userId` fields are ignored.

## Refresh tokens

- Stored as a hash.
- Bound to `UserId` and optional `DeviceInfo`.
- Revoked on logout, on rotation, and when expired.
- Reuse of a revoked token must fail (Phase 2). Rotation policy: issue a new refresh token and revoke the old one.

## Profile

First successful OTP may leave `FullName` empty. `isProfileComplete` is true when `FullName` is non-empty. Profile photo is optional and stored via `IFileStorageService` (`FileCategory.ProfilePhoto`).

## Security notes

- Rate-limit `send-otp` and `verify-otp` (`RateLimiting` options). HTTP 429 uses the v1 envelope.
- Never log JWT access tokens or refresh tokens.
- Production traffic is HTTPS only. The Android production config rejects `http://` API URLs.
