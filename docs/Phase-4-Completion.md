# Phase 4 completion

## Completed

- Re-inspected the mobile TypeScript scaffold, auth APIs, and Android setup docs. Notes recording was not added.
- Copied the official React Native 0.87 Android template into `mobile/SeQrRecall.Mobile/android` and set application id `com.seqr.recall`, display name **SeQr Recall**, component `SeQrRecall`.
- Implemented passwordless login: mobile or email → send OTP → verify `123456` → JWT. New or incomplete profiles must set a full name via `PUT /api/v1/users/me`.
- Access and refresh tokens are stored with `react-native-keychain` (Keystore). They are not written to AsyncStorage.
- HTTP client unwraps the v1 envelope, sends `X-Correlation-ID`, attaches the bearer token, and retries once after refresh-token rotation on 401.
- Debug builds allow cleartext HTTP for `http://10.0.2.2:5080`. Release builds do not.

## Files created

- Native Android tree under `mobile/SeQrRecall.Mobile/android` (package `com.seqr.recall`)
- `mobile/SeQrRecall.Mobile/index.js`, `src/App.tsx`
- API client: `src/api/client.ts`, `src/api/authApi.ts`
- Secure storage: `src/services/secureStorage.ts`, `src/services/tokenSession.ts`
- Auth store: `src/store/authStore.ts`
- Screens: Identifier, OTP, Profile setup, Home placeholder
- Navigation: `src/navigation/RootNavigator.tsx`

## Database changes

None.

## API changes

None. The app calls the Phase 2 auth endpoints.

## Mobile changes

Login, OTP, profile setup, session restore, sign-out. Voice notes remain Phase 5.

## Tests

```text
npx tsc --noEmit                          SUCCESS
dotnet test SeQrRecall.sln                not re-run (backend unchanged)
npx react-native run-android              NOT RUN (no JDK / Android SDK)
```

## Known issues

1. Git is still not on PATH.
2. No JDK, no Android SDK, no `ANDROID_HOME`. The `android/` project exists but cannot be compiled on this PC yet.
3. Development OTP remains `123456` (`DummyOtpService`).
4. In-app home screen is a signed-in placeholder; recording is Phase 5.
5. Physical devices must use the PC LAN IP instead of `10.0.2.2`.

## Next phase

**Phase 5 — Android notes**

Record audio, create a note, upload, poll processing status, show transcript/summary/action items, list and search notes.

Before starting: re-inspect this tree, [docs/AndroidSetup.md](AndroidSetup.md), and the Phase 3 notes APIs.
