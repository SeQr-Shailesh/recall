# Phase 10 completion

## Completed

- Re-inspected CORS, security headers, OTP/upload controllers, and the hardcoded 25 MB / 5 MB caps.
- Upload caps are configuration (`Uploads:AudioMaxBytes`, `Uploads:PhotoMaxBytes`) with a 100 MB hard ceiling on Kestrel, IIS (`web.config`), and request-size attributes.
- Sliding-window rate limits on `send-otp`, `verify-otp`, and audio/photo uploads. HTTP 429 returns the v1 envelope. Development limits are high so local use and the shared integration factory are not blocked.
- Production/Staging: HSTS, HTTPS redirection (already present), stricter CORS (empty allow-list denies browser origins). API responses add CSP, Permissions-Policy, and `Cache-Control: no-store`. CORS exposes `X-Correlation-ID`.
- Production startup logs a warning if `Otp:Provider` is still `Dummy`.

## Files created

- [src/SeQrRecall.Application/Configuration/UploadsOptions.cs](../src/SeQrRecall.Application/Configuration/UploadsOptions.cs)
- [src/SeQrRecall.Application/Configuration/RateLimitOptions.cs](../src/SeQrRecall.Application/Configuration/RateLimitOptions.cs)
- [src/SeQrRecall.Api/RateLimiting/RateLimitPolicyNames.cs](../src/SeQrRecall.Api/RateLimiting/RateLimitPolicyNames.cs)
- [src/SeQrRecall.Api/RateLimiting/RateLimitRejectionWriter.cs](../src/SeQrRecall.Api/RateLimiting/RateLimitRejectionWriter.cs)
- [tests/SeQrRecall.UnitTests/Configuration/UploadsOptionsTests.cs](../tests/SeQrRecall.UnitTests/Configuration/UploadsOptionsTests.cs)
- [tests/SeQrRecall.UnitTests/Api/RateLimitRejectionWriterTests.cs](../tests/SeQrRecall.UnitTests/Api/RateLimitRejectionWriterTests.cs)

## Database changes

None.

## API changes

HTTP 429 for OTP and upload rate limits. Upload size is still validated in the service; the configured cap can change without a rebuild. Response headers are additive.

## Mobile changes

None. Client-side 5 MB photo hint still matches the default server cap.

## Tests

```text
dotnet test SeQrRecall.sln
```

151 passed (120 unit, 31 integration). Mobile `npx tsc --noEmit` is unchanged (no mobile edits).

## Known issues

1. Git is still not on PATH.
2. Integration tests share the Development SQL Server database (`SeQrRecall`) and therefore use Development rate-limit headroom.
3. In-app audio playback of a saved note or conversation is not included.
4. Production should not keep `Otp:Provider = Dummy` for a live deployment.

## Next phase

**Phase 11 — End-to-end acceptance**

Android functional flows and security acceptance (User A cannot read User B's notes, customers, interactions, audio, or photos). Do not start until this phase is accepted.
