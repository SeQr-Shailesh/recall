import * as interactionsApi from '@api/interactionsApi';
import * as leadsApi from '@api/leadsApi';
import * as notesApi from '@api/notesApi';
import {
  getLocalRecording,
  listLocalRecordings,
  removeLocalRecording,
  updateLocalRecording,
  type LocalRecording,
} from '@services/localRecordings';
import { getErrorMessage, isAlreadyUploadedError, isLikelyOfflineError, OFFLINE_SAVED_MESSAGE } from '@utils/errors';
import type { NoteStatus } from '@models/index';

const inFlight = new Set<string>();

export type ProcessRecordingResult =
  | { status: 'uploaded'; remoteId: string }
  | { status: 'pending'; message: string; offline: boolean };

export interface SyncPendingResult {
  attempted: number;
  uploaded: number;
  remaining: number;
  offline: boolean;
  lastError: string | null;
}

export type SubmitRecordingResult =
  | {
      status: 'ready';
      remoteId: string;
      processingStatus: NoteStatus['processingStatus'];
      processingError: string | null;
    }
  | { status: 'uploaded-waiting'; remoteId: string }
  | { status: 'saved-locally'; localId: string; message: string };

export async function submitNoteRecording(
  recording: LocalRecording,
  onStatus?: (line: string) => void,
): Promise<SubmitRecordingResult> {
  const uploaded = await processLocalRecording(recording.id);
  if (uploaded.status !== 'uploaded') {
    return savedLocally(recording.id, uploaded);
  }

  return waitForNote(uploaded.remoteId, onStatus);
}

export async function submitInteractionRecording(
  recording: LocalRecording,
  onStatus?: (line: string) => void,
): Promise<SubmitRecordingResult> {
  const uploaded = await processLocalRecording(recording.id);
  if (uploaded.status !== 'uploaded') {
    return savedLocally(recording.id, uploaded);
  }

  return waitForInteraction(uploaded.remoteId, onStatus);
}

export async function submitLeadRecording(
  recording: LocalRecording,
  onStatus?: (line: string) => void,
): Promise<SubmitRecordingResult> {
  const uploaded = await processLocalRecording(recording.id);
  if (uploaded.status !== 'uploaded') {
    return savedLocally(recording.id, uploaded);
  }

  return waitForLead(uploaded.remoteId, onStatus);
}

export async function syncPendingRecordings(): Promise<SyncPendingResult> {
  const items = await listLocalRecordings();
  let uploaded = 0;
  let lastError: string | null = null;
  let offline = false;

  for (const item of items) {
    const result = await processLocalRecording(item.id);
    if (result.status === 'uploaded') {
      uploaded += 1;
      continue;
    }

    lastError = result.message;
    offline = result.offline;
    if (result.offline) {
      break;
    }
  }

  const remaining = (await listLocalRecordings()).length;
  return {
    attempted: items.length,
    uploaded,
    remaining,
    offline,
    lastError,
  };
}

export async function processLocalRecording(id: string): Promise<ProcessRecordingResult> {
  if (inFlight.has(id)) {
    return { status: 'pending', message: 'This recording is already being saved.', offline: false };
  }

  inFlight.add(id);
  try {
    const item = await getLocalRecording(id);
    if (!item) {
      return {
        status: 'pending',
        message: 'This recording is no longer on this phone.',
        offline: false,
      };
    }

    const remoteId = await ensureRemoteId(item);
    const latest = (await getLocalRecording(id)) ?? { ...item, remoteId };

    if (!latest.audioUploaded) {
      await uploadAudio(latest, remoteId);
      await updateLocalRecording(latest.id, { audioUploaded: true, lastError: null });
    }

    const withAudio = (await getLocalRecording(id)) ?? latest;
    if (withAudio.photoPath && !withAudio.photoUploaded) {
      await uploadPhoto(withAudio, remoteId);
      await updateLocalRecording(withAudio.id, { photoUploaded: true, lastError: null });
    }

    await removeLocalRecording(item.id);
    return { status: 'uploaded', remoteId };
  } catch (caught) {
    const message = getErrorMessage(caught, 'Unable to send this recording yet.');
    const current = await getLocalRecording(id);
    await updateLocalRecording(id, {
      lastError: message,
      uploadAttempts: (current?.uploadAttempts ?? 0) + 1,
    });
    return { status: 'pending', message, offline: isLikelyOfflineError(caught) };
  } finally {
    inFlight.delete(id);
  }
}

function savedLocally(
  localId: string,
  result: Extract<ProcessRecordingResult, { status: 'pending' }>,
): SubmitRecordingResult {
  return {
    status: 'saved-locally',
    localId,
    message: result.offline ? OFFLINE_SAVED_MESSAGE : result.message,
  };
}

async function ensureRemoteId(item: LocalRecording): Promise<string> {
  if (item.remoteId) {
    return item.remoteId;
  }

  if (item.kind === 'interaction') {
    if (!item.customerId) {
      throw new Error('This conversation is missing a customer.');
    }

    const created = await interactionsApi.createInteraction(item.customerId);
    await updateLocalRecording(item.id, { remoteId: created.id, lastError: null });
    return created.id;
  }

  if (item.kind === 'lead') {
    const created = await leadsApi.createLead();
    await updateLocalRecording(item.id, { remoteId: created.id, lastError: null });
    return created.id;
  }

  const created = await notesApi.createNote();
  await updateLocalRecording(item.id, { remoteId: created.id, lastError: null });
  return created.id;
}

async function uploadAudio(item: LocalRecording, remoteId: string): Promise<void> {
  try {
    await sendAudio(item, remoteId);
  } catch (error) {
    if (isAlreadyUploadedError(error)) {
      return;
    }

    throw error;
  }
}

async function sendAudio(item: LocalRecording, remoteId: string): Promise<void> {
  const file = {
    uri: item.filePath,
    name: item.fileName,
    type: item.contentType,
    durationSeconds: item.durationSeconds,
  };

  if (item.kind === 'interaction') {
    await interactionsApi.uploadInteractionAudio(remoteId, file);
    return;
  }

  if (item.kind === 'lead') {
    await leadsApi.uploadLeadAudio(remoteId, file);
    return;
  }

  await notesApi.uploadNoteAudio(remoteId, file);
}

async function uploadPhoto(item: LocalRecording, remoteId: string): Promise<void> {
  if (item.kind !== 'lead' || !item.photoPath) {
    return;
  }

  await leadsApi.uploadLeadPhoto(remoteId, {
    uri: item.photoPath,
    name: item.photoName ?? 'lead.jpg',
    type: item.photoType ?? 'image/jpeg',
  });
}

async function waitForNote(
  remoteId: string,
  onStatus?: (line: string) => void,
): Promise<SubmitRecordingResult> {
  try {
    const terminal = await notesApi.waitForNoteTerminalStatus(remoteId, (status) => {
      if (status.processingStatus !== 'Completed' && status.processingStatus !== 'Failed') {
        onStatus?.('Understanding your note...');
      }
    });
    return {
      status: 'ready',
      remoteId,
      processingStatus: terminal.processingStatus,
      processingError: terminal.processingError,
    };
  } catch {
    return { status: 'uploaded-waiting', remoteId };
  }
}

async function waitForInteraction(
  remoteId: string,
  onStatus?: (line: string) => void,
): Promise<SubmitRecordingResult> {
  try {
    const terminal = await interactionsApi.waitForInteractionTerminalStatus(remoteId, (status) => {
      if (status.processingStatus !== 'Completed' && status.processingStatus !== 'Failed') {
        onStatus?.('Understanding this conversation...');
      }
    });
    return {
      status: 'ready',
      remoteId,
      processingStatus: terminal.processingStatus,
      processingError: terminal.processingError,
    };
  } catch {
    return { status: 'uploaded-waiting', remoteId };
  }
}

async function waitForLead(
  remoteId: string,
  onStatus?: (line: string) => void,
): Promise<SubmitRecordingResult> {
  try {
    const terminal = await leadsApi.waitForLeadTerminalStatus(remoteId, (status) => {
      if (status.processingStatus !== 'Completed' && status.processingStatus !== 'Failed') {
        onStatus?.('Understanding this lead...');
      }
    });
    return {
      status: 'ready',
      remoteId,
      processingStatus: terminal.processingStatus,
      processingError: terminal.processingError,
    };
  } catch {
    return { status: 'uploaded-waiting', remoteId };
  }
}
