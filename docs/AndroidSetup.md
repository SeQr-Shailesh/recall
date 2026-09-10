# Android setup

The mobile app is **bare React Native 0.87** with TypeScript. iOS is not built in the initial phases.

Application id: `com.seqr.recall`. Display name: **SeQr Recall**. Component name: `SeQrRecall`.

## Phase 5+ status

`mobile/SeQrRecall.Mobile` now contains:

- Native `android/` project (RN 0.87 template, package `com.seqr.recall`)
- Login, OTP, profile setup, signed-in notes, and customers
- Record (AAC/M4A), upload, poll status, list/search, details, delete
- Customer list/search/create/edit/details/delete and optional photo
- Customer conversation timeline, record, details, and optional photo
- Microphone permission requested only when recording starts
- Camera permission requested only when taking a customer photo
- JWT in Android Keystore via `react-native-keychain`

JDK 17 and the Android SDK are installed on this development PC. Open a **new** terminal (or restart Cursor) so `JAVA_HOME` and `ANDROID_HOME` are on PATH.

## Toolchain (required to run on a device or emulator)

Installed locally (user environment):

| Item | Location |
| --- | --- |
| JDK 17 | `C:\Program Files\Microsoft\jdk-17.*-hotspot` (`JAVA_HOME`; the patch version changes as the JDK auto-updates) |
| Android SDK | `%LOCALAPPDATA%\Android\Sdk` (`ANDROID_HOME`) |
| Emulator AVD | `Pixel_API_34` (API 34, Google APIs, x86_64) |

SDK packages: platform-tools, platforms 36 and 37, build-tools 37.0.0, NDK 27.1.12297006, CMake 3.30.5, emulator.

Android Studio is optional (SDK was installed with command-line tools). Confirm in a new terminal:

```powershell
node --version
java -version
adb version
```

## Typecheck

```powershell
cd mobile/SeQrRecall.Mobile
npm install
npx tsc --noEmit
```

## Run on Android (after JDK + SDK)

Start the API first (`dotnet run --project src/SeQrRecall.Api`). Start the emulator (or plug in a device with USB debugging):

```powershell
emulator -avd Pixel_API_34
```

Then:

```powershell
cd mobile/SeQrRecall.Mobile
npm install
npx react-native run-android --active-arch-only
```

`--active-arch-only` builds only the emulator/device ABI (x86_64 on `Pixel_API_34`). Omit it when producing a multi-ABI APK.

If Gradle fails with `Filename longer than 260 characters`, the app is pinned to CMake 3.30.5 for that reason. Delete stale CMake output and retry:

```powershell
Remove-Item -Recurse -Force android\app\.cxx, android\app\build -ErrorAction SilentlyContinue
npx react-native run-android --active-arch-only
```

Development OTP is `123456`. Debug builds allow cleartext HTTP so the emulator can reach `http://10.0.2.2:5080`. Release builds disable cleartext traffic.

## Build the debug APK

```powershell
cd mobile/SeQrRecall.Mobile/android
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
.\gradlew.bat assembleDebug
```

The APK is written under `android/app/build/outputs/apk/debug/`. A debug APK does **not** embed the JS bundle, so it only runs with Metro reachable. Use it for the development loop, not for handing to a tester.

If Gradle reports `JAVA_HOME is set to an invalid directory`, the JDK was updated to a new patch version. Point `JAVA_HOME` at the folder that exists:

```powershell
$env:JAVA_HOME = (Get-ChildItem "C:\Program Files\Microsoft" -Directory -Filter "jdk-17*-hotspot").FullName
```

## Build the release APK (the one testers install)

```powershell
cd mobile/SeQrRecall.Mobile/android
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
.\gradlew.bat assembleRelease
```

The APK is written to `android/app/build/outputs/apk/release/app-release.apk`. It embeds the Hermes bundle, so it installs and runs with no dev server, and `appConfig` points it at the production HTTPS API.

It is signed with `debug.keystore`, which is fine for sideloading but must be replaced with a real upload key before any Play Store release. See [signed-apk-android](https://reactnative.dev/docs/signed-apk-android).

Native build output for the app and for third-party native modules is staged under `android/.cxx/`. Keeping it out of `node_modules/<module>/android/.cxx` is what keeps prefab paths inside the 260-character Windows limit; a release build otherwise fails with `ninja: error: manifest 'build.ninja' still dirty after 100 tries`.

## Launcher icons

`ic_launcher.png` and `ic_launcher_round.png` in every `mipmap-*` folder are generated from `src/assets/logo.png`. Regenerate them after replacing that logo:

```powershell
cd mobile/SeQrRecall.Mobile
powershell -ExecutionPolicy Bypass -File scripts\generate-launcher-icons.ps1
```

## Environment URLs

| Environment | API base URL |
| --- | --- |
| Development (emulator) | `http://10.0.2.2:5080` |
| Development (device) | `http://<dev-machine-lan-ip>:5080` |
| Production | `https://...` only |

`getAppConfig('production')` throws if the URL is not HTTPS.

## Permissions (Phase 5+)

Request only when needed:

- Microphone — start recording (Phase 5)
- Camera — take a customer or conversation photo. The photo library picker does not request camera.

Handle granted, denied, and permanently denied with a clear explanation. No facial recognition. Customer photos are JPEG, PNG, or WebP, max 5 MB.

## Secure storage

Access and refresh tokens go in Android Keystore-backed storage (`react-native-keychain`), never plain AsyncStorage.

## Recording UX copy

Use user language: "Tap to speak", "Listening...", "Understanding your note...", "Your note is ready". Do not show "STT processing". Recording is implemented in Phase 5.
