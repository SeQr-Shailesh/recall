# Troubleshooting

## Backend will not start

- Confirm `dotnet --version` is 10.x.
- Restore: `dotnet restore SeQrRecall.sln`.
- User-secrets / env vars: empty `Jwt:Secret` will fail token issuance from Phase 2 onward. Phase 0 does not issue tokens.

## Database

- SQL Server service `MSSQLSERVER` should be Running.
- `Trusted_Connection=True` needs a Windows login that can access `SeQrRecall`.
- `TrustServerCertificate=True` is for local dev only.
- If tables are missing, restore tools and apply migrations:

```powershell
dotnet tool restore
dotnet ef database update --project src/SeQrRecall.Infrastructure --startup-project src/SeQrRecall.Api
```

## File storage

- Development uses `%LOCALAPPDATA%\SeQrRecallData` because this repo's development host may have no `D:` drive.
- Production uses `D:\SeQrRecallData`. If IIS returns 500 on upload, check app-pool NTFS permissions on that folder.

## Android

- `npx tsc --noEmit` should succeed after `npm install`.
- `npx react-native run-android --active-arch-only` needs JDK 17, Android SDK, and `ANDROID_HOME`. They are installed on this PC (`JAVA_HOME`, `ANDROID_HOME`). Open a new terminal if `java` or `adb` is not recognized. Start the emulator with `emulator -avd Pixel_API_34` or connect a device with USB debugging.
- `Filename longer than 260 characters` during `:app:buildCMakeDebug` is a Windows MAX_PATH failure from CMake 3.22 object paths. The app uses CMake 3.30.5 and `CMAKE_OBJECT_PATH_MAX`. Delete `android/app/.cxx` and `android/app/build`, then rebuild. Do not use CMake 3.22.1 for this module.
- `Failed to get the SHA-1 for ... metro-runtime ... require.js` is Metro's file map missing that file. Stop Metro (Ctrl+C), then from `mobile/SeQrRecall.Mobile` run `npx react-native start --reset-cache`. The `metro.config.js` excludes `android/app/.cxx` from the crawl and disables Watchman. A warning about `ReactNativeFeatureFlags` package exports is harmless.
- Emulator cannot reach `localhost` of the host; use `10.0.2.2`.
- Physical device needs the PC LAN IP and firewall opening on 5080/7080.
- Tokens are in Keystore (`react-native-keychain`), not AsyncStorage.

## OTP

- UI always pretends the OTP was sent.
- Only `123456` validates with `DummyOtpService`.
- If OTP "is not received", check Development logs only. Production must not log OTP values.

## STT / AI

- Mobile never talks to Sarvam or OpenAI. Failures show as note status `Failed` or `Completed` with `processingError`.
- `Speech:Provider = Sarvam` without `Speech:ApiKey` prevents the host from starting. Development can stay on `Mock`.
- `AI:Provider = OpenAI` or `Gemini` without `AI:ApiKey` prevents the host from starting. `AI:Provider = Qwen` does not require a key. Development can stay on `Mock`.
- If STT succeeds and AI fails, the note is `Completed` with a transcript and `processingError` explaining that the summary could not be generated.
- Timeouts and invalid provider JSON are `ExternalProviderException` — clients see a generic message.

## Git

- This development environment may not have `git` on PATH. Install Git for Windows to initialize the repo. `.gitignore` is already in the tree.
