import Sound, {
  AudioEncoderAndroidType,
  AudioSourceAndroidType,
  OutputFormatAndroidType,
} from 'react-native-nitro-sound';
import RNFS from 'react-native-fs';
import { MAX_RECORDING_SECONDS } from '@constants/index';
import { ensureRecordingsDirectory, getRecordingsDirectory, toFsPath } from '@services/localRecordings';

export interface FinishedRecording {
  uri: string;
  durationSeconds: number;
  contentType: string;
  fileName: string;
}

let lastPositionMs = 0;

export async function startRecording(): Promise<void> {
  lastPositionMs = 0;
  const directory = await ensureRecordingsDirectory();
  const path = `${directory}/in-progress-${Date.now()}.m4a`;
  Sound.addRecordBackListener((event) => {
    lastPositionMs = event.currentPosition;
  });
  await Sound.startRecorder(path, {
    AudioEncoderAndroid: AudioEncoderAndroidType.AAC,
    AudioSourceAndroid: AudioSourceAndroidType.MIC,
    OutputFormatAndroid: OutputFormatAndroidType.MPEG_4,
    AudioEncodingBitRate: 128000,
    AudioSamplingRate: 44100,
    AudioChannels: 1,
  });
}

export async function stopRecording(): Promise<FinishedRecording> {
  const uri = await Sound.stopRecorder();
  Sound.removeRecordBackListener();
  return {
    uri,
    durationSeconds: clampRecordingSeconds(Math.round(lastPositionMs / 1000) || 1),
    contentType: 'audio/mp4',
    fileName: 'note.m4a',
  };
}

export async function cancelRecording(): Promise<void> {
  try {
    const uri = await Sound.stopRecorder();
    Sound.removeRecordBackListener();
    await deleteRecordingFile(uri);
  } catch {
    // Already stopped or never started.
  }
}

export async function deleteRecordingFile(uri: string): Promise<void> {
  try {
    const path = toFsPath(uri);
    if (!path.startsWith(getRecordingsDirectory())) {
      return;
    }

    if (await RNFS.exists(path)) {
      await RNFS.unlink(path);
    }
  } catch {
    // Local cleanup is best-effort.
  }
}

export function clampRecordingSeconds(seconds: number): number {
  return Math.min(Math.max(seconds, 1), MAX_RECORDING_SECONDS);
}
