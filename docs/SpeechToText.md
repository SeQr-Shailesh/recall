# Speech-to-text

All STT traffic goes through `ISpeechToTextService.TranscribeAsync`. The rest of the application does not know whether Sarvam, Google, Azure, Deepgram, or Whisper ran. The mobile app never calls Sarvam.

## Contract

Request: audio stream, content type, optional original file name (for logging only; never sent to the vendor and never used as a storage name), optional duration.

Result:

- `EnglishTranscript` — meaning-preserving English, not word-for-word if that would sound unnatural
- `DetectedLanguages`
- optional provider raw text (not exposed to mobile)

## Indian language requirement

A single recording may mix English, Indian English, Hindi, Marathi, Gujarati, and Hinglish.

Example spoken:

> "Rajesh sir ke saath aaj meeting hui. Unko hamara inventory system pasand aaya. He asked me to send the quotation by Friday."

Stored English:

> "I had a meeting with Rajesh today. He liked our inventory system. He asked me to send the quotation by Friday."

Preserve names, companies, products, numbers, dates, commitments, quantities, locations.

## Sarvam (Phase 6)

`Speech:Provider = Sarvam` registers `SarvamSpeechToTextService`. Confirmed against Sarvam's published docs (Saaras v3, August 2026):

| Item | Value |
| --- | --- |
| Auth | Header `api-subscription-key` (403 + `invalid_api_key_error` when the key is missing or invalid) |
| Model | `Speech:Model` (default `saaras:v3`) |
| Mode | Always `translate` so the contract returns English |
| Language | `unknown` (auto-detect; mixed Indic + English) |
| REST | `POST https://api.sarvam.ai/speech-to-text` multipart `file` — audio up to 30 seconds |
| Batch | Job API when `durationSeconds` is greater than 30, or when REST rejects the file as too long |

Batch flow (published job API):

1. `POST /speech-to-text/job/v1` with `job_parameters` (`model`, `mode=translate`, `language_code=unknown`)
2. `POST /speech-to-text/job/v1/upload-files` → presigned `upload_urls`
3. `PUT` the audio to the signed URL (Azure Blob uploads include `x-ms-blob-type: BlockBlob`)
4. `POST /speech-to-text/job/v1/{job_id}/start`
5. Poll `GET /speech-to-text/job/v1/{job_id}/status` until `Completed` or `Failed`
6. `POST /speech-to-text/job/v1/download-files` then GET the result JSON

Timeout, invalid JSON, empty transcript, HTTP 403/429/503, and outages throw `ExternalProviderException`. API keys, audio bytes, and transcripts are not written to exception messages.

Development JSON stays `Speech:Provider = Mock`. Production JSON is `Sarvam`; set `Speech:ApiKey` (and optionally `Speech:BaseUrl` / `Speech:Model`) via environment variables or IIS. Startup fails if the provider is `Sarvam` and the API key is empty.

## Audio format

Prefer AAC/M4A (`audio/mp4`). The Android app records MPEG-4 AAC and uploads field `file` as `note.m4a`. Temporary files are deleted after a successful upload. The vendor multipart filename is a synthetic `audio.m4a` (or matching extension), never the client name.

## Mocks

`Speech:Provider = Mock` registers `MockSpeechToTextService`. It is labeled in logs and in `ProviderName`. It does not call a vendor and must not be used as if it were production transcription.
