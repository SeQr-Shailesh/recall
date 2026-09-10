# Phase 11 completion

## Completed

- Re-inspected note, customer, and interaction IDOR coverage. Gaps were status endpoints, interaction audio/photo GET, list/search leakage, client-supplied `userId` JSON, and unauthenticated file streams.
- `OwnershipIsolationTests` is the security acceptance: User B receives a generic 404 for User A's notes, customers, interactions, audio, and photos. Search lists do not include the other user's ids. Extra `userId` / `id` JSON does not change ownership. Files are not served from static disk paths. Invalid or missing JWT on file streams is 401.
- `CurrentUserServiceTests` locks `UserId` to JWT `NameIdentifier` / `sub`. An `X-User-Id` header is ignored.
- `AndroidFunctionalFlowTests` exercises the same API sequence the Android app uses: login (OTP `123456`), profile, note create/upload (AAC/M4A) / poll / search / details, customer photo, conversation photo + audio + timeline.
- Product code for notes, STT, AI, customers, and conversations was not changed.

## Files created

- [tests/SeQrRecall.UnitTests/Security/CurrentUserServiceTests.cs](../tests/SeQrRecall.UnitTests/Security/CurrentUserServiceTests.cs)
- [tests/SeQrRecall.IntegrationTests/AcceptanceSupport.cs](../tests/SeQrRecall.IntegrationTests/AcceptanceSupport.cs)
- [tests/SeQrRecall.IntegrationTests/OwnershipIsolationTests.cs](../tests/SeQrRecall.IntegrationTests/OwnershipIsolationTests.cs)
- [tests/SeQrRecall.IntegrationTests/AndroidFunctionalFlowTests.cs](../tests/SeQrRecall.IntegrationTests/AndroidFunctionalFlowTests.cs)

## Database changes

None.

## API changes

None. Acceptance tests only.

## Mobile changes

None. The Android debug APK is built as a compile check (`gradlew assembleDebug`).

## Tests

```text
dotnet test SeQrRecall.sln
npx tsc --noEmit
gradlew assembleDebug -PreactNativeArchitectures=x86_64
```

158 passed (124 unit, 34 integration). Mobile `npx tsc --noEmit` succeeded. `gradlew assembleDebug -PreactNativeArchitectures=x86_64` succeeded (BUILD SUCCESSFUL, 16m).

## Known issues

1. Git is still not on PATH.
2. Integration tests share the Development SQL Server database (`SeQrRecall`).
3. In-app audio playback of a saved note or conversation is not included.
4. Production should not keep `Otp:Provider = Dummy` for a live deployment.
5. Emulator UI (microphone/camera on `Pixel_API_34`) is not automated; the functional flow is asserted against the same HTTP paths the app uses.

## Next phase

v1 is complete. Optional later work (not started): iOS client, real OTP provider, Hangfire, in-app audio playback.
