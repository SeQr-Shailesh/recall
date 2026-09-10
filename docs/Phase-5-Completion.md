# Phase 5 completion

## Completed

- Re-inspected Phase 4 auth, notes APIs, and Android setup. Customers were not added.
- Notes list with search, pull-to-refresh, and paging. Empty state points at recording.
- Recording: microphone is requested only on start. Copy is "Tap to speak", "Listening...", "Understanding your note...", "Your note is ready". Denied and permanently denied states are explained; Settings can be opened when blocked.
- Flow: create draft note → upload AAC/M4A → poll status → open details. Local cache files are deleted after a successful upload.
- Details show title, summaries, transcript, action items, and processing errors (including completed-with-AI-failure). Soft delete is available.
- Audio is recorded with `react-native-nitro-sound` as MPEG-4 AAC (`audio/mp4`).

## Files created

- [mobile/SeQrRecall.Mobile/src/api/notesApi.ts](../mobile/SeQrRecall.Mobile/src/api/notesApi.ts)
- [mobile/SeQrRecall.Mobile/src/services/audioRecorder.ts](../mobile/SeQrRecall.Mobile/src/services/audioRecorder.ts)
- [mobile/SeQrRecall.Mobile/src/services/microphonePermission.ts](../mobile/SeQrRecall.Mobile/src/services/microphonePermission.ts)
- [mobile/SeQrRecall.Mobile/src/store/notesStore.ts](../mobile/SeQrRecall.Mobile/src/store/notesStore.ts)
- [mobile/SeQrRecall.Mobile/src/screens/notes/NotesListScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/notes/NotesListScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/screens/notes/RecordScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/notes/RecordScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/screens/notes/NoteDetailsScreen.tsx](../mobile/SeQrRecall.Mobile/src/screens/notes/NoteDetailsScreen.tsx)
- [mobile/SeQrRecall.Mobile/src/utils/noteStatus.ts](../mobile/SeQrRecall.Mobile/src/utils/noteStatus.ts)

`RECORD_AUDIO` was added to the Android manifest. The HTTP client now supports multipart uploads and DELETE.

## Database changes

None.

## API changes

None. The app calls the Phase 3 notes endpoints.

## Mobile changes

Voice notes: record, upload, poll, list, search, details, delete. Customer Recall remains later.

## Tests

```text
npx tsc --noEmit                          SUCCESS
dotnet test SeQrRecall.sln                not re-run (backend unchanged)
npx react-native run-android              NOT RUN during Phase 5 (JDK/SDK were missing then)
```

## Known issues

1. Git is still not on PATH.
2. JDK 17 and Android SDK are now installed (`JAVA_HOME`, `ANDROID_HOME`, AVD `Pixel_API_34`). Open a new terminal before `npx react-native run-android`.
3. Development STT/AI are still labeled mocks. Real transcription is Phase 6.
4. In-app audio playback of a saved note is not included (upload + details only).
5. Physical devices must use the PC LAN IP instead of `10.0.2.2`.

## Next phase

**Phase 6 — Sarvam speech-to-text**

Replace `MockSpeechToTextService` with `SarvamSpeechToTextService` behind `Speech:Provider = Sarvam`. Confirm the current Sarvam API from published docs. Keep the notes pipeline and mobile app unchanged.

Before starting: re-inspect [docs/SpeechToText.md](SpeechToText.md) and the `ISpeechToTextService` contract.
