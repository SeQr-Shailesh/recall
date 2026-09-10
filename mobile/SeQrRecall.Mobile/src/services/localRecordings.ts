import RNFS from 'react-native-fs';
import { createCorrelationId } from '@utils/correlationId';

interface RecordingToKeep {
  uri: string;
  durationSeconds: number;
  contentType: string;
  fileName: string;
}

export type LocalRecordingKind = 'note' | 'interaction' | 'lead';

export type LocalRecordingMeta =
  | { kind: 'note' }
  | { kind: 'interaction'; customerId: string }
  | { kind: 'lead'; photo?: LocalPhoto | null };

/** A photo picked before the lead exists on the server. Held next to the audio until both can be sent. */
export interface LocalPhoto {
  uri: string;
  name: string;
  type: string;
}

export interface LocalRecording {
  id: string;
  kind: LocalRecordingKind;
  customerId: string | null;
  remoteId: string | null;
  filePath: string;
  fileName: string;
  contentType: string;
  durationSeconds: number;
  createdOn: string;
  lastError: string | null;
  uploadAttempts: number;
  audioUploaded: boolean;
  photoPath: string | null;
  photoName: string | null;
  photoType: string | null;
  photoUploaded: boolean;
}

const RECORDINGS_DIR = `${RNFS.DocumentDirectoryPath}/seqr-recordings`;
const QUEUE_PATH = `${RECORDINGS_DIR}/queue.json`;
const QUEUE_BACKUP_PATH = `${RECORDINGS_DIR}/queue.bak.json`;
const QUEUE_TEMP_PATH = `${RECORDINGS_DIR}/queue.json.tmp`;

let queueLock: Promise<void> = Promise.resolve();

export function getRecordingsDirectory(): string {
  return RECORDINGS_DIR;
}

export function toFsPath(uri: string): string {
  return uri.replace(/^file:\/\//, '');
}

export async function ensureRecordingsDirectory(): Promise<string> {
  if (!(await RNFS.exists(RECORDINGS_DIR))) {
    await RNFS.mkdir(RECORDINGS_DIR);
  }

  return RECORDINGS_DIR;
}

export async function enqueueLocalRecording(
  recording: RecordingToKeep,
  meta: LocalRecordingMeta,
): Promise<LocalRecording> {
  return withQueue(async () => {
    await ensureRecordingsDirectory();
    const id = createCorrelationId();
    const filePath = `${RECORDINGS_DIR}/${id}.m4a`;
    const source = toFsPath(recording.uri);
    if (source !== filePath) {
      if (await RNFS.exists(filePath)) {
        await RNFS.unlink(filePath);
      }
      await RNFS.moveFile(source, filePath);
    }

    const photo = meta.kind === 'lead' ? await copyPhotoUnlocked(id, meta.photo ?? null) : null;
    const item: LocalRecording = {
      id,
      kind: meta.kind,
      customerId: meta.kind === 'interaction' ? meta.customerId : null,
      remoteId: null,
      filePath,
      fileName: audioFileNameFor(meta.kind),
      contentType: recording.contentType,
      durationSeconds: recording.durationSeconds,
      createdOn: new Date().toISOString(),
      lastError: null,
      uploadAttempts: 0,
      audioUploaded: false,
      photoPath: photo?.path ?? null,
      photoName: photo?.name ?? null,
      photoType: photo?.type ?? null,
      photoUploaded: false,
    };
    const items = await readQueueUnlocked();
    items.unshift(item);
    await writeQueueUnlocked(items);
    return item;
  });
}

export async function attachLocalPhoto(id: string, photo: LocalPhoto): Promise<LocalRecording | null> {
  return withQueue(async () => {
    const items = await readQueueUnlocked();
    const index = items.findIndex((item) => item.id === id);
    if (index < 0) {
      return null;
    }

    const previous = items[index].photoPath;
    const copied = await copyPhotoUnlocked(id, photo);
    if (previous && previous !== copied?.path) {
      await deleteFileIfExists(previous);
    }

    const updated: LocalRecording = {
      ...items[index],
      photoPath: copied?.path ?? null,
      photoName: copied?.name ?? null,
      photoType: copied?.type ?? null,
      photoUploaded: false,
    };
    items[index] = updated;
    await writeQueueUnlocked(items);
    return updated;
  });
}

function audioFileNameFor(kind: LocalRecordingKind): string {
  switch (kind) {
    case 'interaction':
      return 'conversation.m4a';
    case 'lead':
      return 'lead.m4a';
    default:
      return 'note.m4a';
  }
}

async function copyPhotoUnlocked(
  id: string,
  photo: LocalPhoto | null,
): Promise<{ path: string; name: string; type: string } | null> {
  if (!photo) {
    return null;
  }

  const extension = photo.name.match(/\.(jpe?g|png|webp)$/i)?.[0] ?? '.jpg';
  const path = `${RECORDINGS_DIR}/${id}-photo${extension}`;
  const source = toFsPath(photo.uri);
  if (source !== path) {
    if (await RNFS.exists(path)) {
      await RNFS.unlink(path);
    }
    await RNFS.copyFile(source, path);
  }

  return { path, name: photo.name, type: photo.type };
}

export async function listLocalRecordings(): Promise<LocalRecording[]> {
  return withQueue(async () => {
    await ensureRecordingsDirectory();
    await recoverOrphanFilesUnlocked();
    return readQueueUnlocked();
  });
}

export async function getLocalRecording(id: string): Promise<LocalRecording | null> {
  const items = await listLocalRecordings();
  return items.find((item) => item.id === id) ?? null;
}

export async function updateLocalRecording(
  id: string,
  patch: Partial<
    Pick<LocalRecording, 'remoteId' | 'lastError' | 'uploadAttempts' | 'audioUploaded' | 'photoUploaded'>
  >,
): Promise<LocalRecording | null> {
  return withQueue(async () => {
    const items = await readQueueUnlocked();
    const index = items.findIndex((item) => item.id === id);
    if (index < 0) {
      return null;
    }

    const updated = { ...items[index], ...patch };
    items[index] = updated;
    await writeQueueUnlocked(items);
    return updated;
  });
}

export async function removeLocalRecording(id: string): Promise<void> {
  await withQueue(async () => {
    const items = await readQueueUnlocked();
    const item = items.find((entry) => entry.id === id);
    const remaining = items.filter((entry) => entry.id !== id);
    await writeQueueUnlocked(remaining);
    if (item) {
      await deleteFileIfExists(item.filePath);
      if (item.photoPath) {
        await deleteFileIfExists(item.photoPath);
      }
    }
  });
}

async function recoverOrphanFilesUnlocked(): Promise<void> {
  const items = await readQueueUnlocked();
  const files = await RNFS.readDir(RECORDINGS_DIR);

  const existing = await Promise.all(
    items.map(async (item) => ({
      item,
      exists: await RNFS.exists(toFsPath(item.filePath)),
    })),
  );
  const kept = existing.filter((entry) => entry.exists).map((entry) => entry.item);
  if (kept.length !== items.length) {
    await writeQueueUnlocked(kept);
  }

  const keptPhotos = new Set(
    kept.map((item) => item.photoPath).filter((path): path is string => path !== null).map(toFsPath),
  );
  for (const file of files) {
    if (file.name.includes('-photo.') && !keptPhotos.has(toFsPath(file.path))) {
      await deleteFileIfExists(file.path);
    }
  }
}

async function withQueue<T>(work: () => Promise<T>): Promise<T> {
  const previous = queueLock;
  let release: () => void = () => undefined;
  queueLock = new Promise<void>((resolve) => {
    release = resolve;
  });
  await previous;
  try {
    return await work();
  } finally {
    release();
  }
}

async function readQueueUnlocked(): Promise<LocalRecording[]> {
  const fromPrimary = await readQueueFile(QUEUE_PATH);
  if (fromPrimary) {
    return fromPrimary;
  }

  const fromBackup = await readQueueFile(QUEUE_BACKUP_PATH);
  if (fromBackup) {
    await writeQueueUnlocked(fromBackup);
    return fromBackup;
  }

  return [];
}

async function readQueueFile(path: string): Promise<LocalRecording[] | null> {
  if (!(await RNFS.exists(path))) {
    return null;
  }

  try {
    const raw = await RNFS.readFile(path, 'utf8');
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return null;
    }

    return parsed.filter(isLocalRecording).map(normalize);
  } catch {
    return null;
  }
}

/** Queue entries written by older builds have no upload-progress or photo fields. */
function normalize(item: LocalRecording): LocalRecording {
  return {
    ...item,
    audioUploaded: item.audioUploaded ?? false,
    photoPath: item.photoPath ?? null,
    photoName: item.photoName ?? null,
    photoType: item.photoType ?? null,
    photoUploaded: item.photoUploaded ?? false,
  };
}

async function writeQueueUnlocked(items: LocalRecording[]): Promise<void> {
  const payload = JSON.stringify(items);
  await RNFS.writeFile(QUEUE_TEMP_PATH, payload, 'utf8');
  if (await RNFS.exists(QUEUE_PATH)) {
    await RNFS.unlink(QUEUE_PATH);
  }
  await RNFS.moveFile(QUEUE_TEMP_PATH, QUEUE_PATH);
  await RNFS.writeFile(QUEUE_BACKUP_PATH, payload, 'utf8');
}

async function deleteFileIfExists(uri: string): Promise<void> {
  try {
    const path = toFsPath(uri);
    if (await RNFS.exists(path)) {
      await RNFS.unlink(path);
    }
  } catch {
    // Local cleanup is best-effort.
  }
}

function isLocalRecording(value: unknown): value is LocalRecording {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const item = value as LocalRecording;
  return (
    typeof item.id === 'string' &&
    (item.kind === 'note' || item.kind === 'interaction' || item.kind === 'lead') &&
    typeof item.filePath === 'string' &&
    typeof item.fileName === 'string' &&
    typeof item.contentType === 'string' &&
    typeof item.durationSeconds === 'number' &&
    typeof item.createdOn === 'string'
  );
}
