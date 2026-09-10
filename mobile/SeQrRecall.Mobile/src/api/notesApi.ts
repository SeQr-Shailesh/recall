import { api } from '@api/client';
import {
  DEFAULT_PAGE_SIZE,
  NOTE_POLL_INTERVAL_MS,
  NOTE_POLL_MAX_ATTEMPTS,
} from '@constants/index';
import type {
  CreateNoteResponse,
  NoteDetails,
  NoteListItem,
  NoteStatus,
  PagedRequest,
  PagedResult,
} from '@models/index';

export interface RecordingUpload {
  uri: string;
  name: string;
  type: string;
  durationSeconds: number;
}

export function listNotes(request: PagedRequest = {}): Promise<PagedResult<NoteListItem>> {
  const pageNumber = request.pageNumber ?? 1;
  const pageSize = request.pageSize ?? DEFAULT_PAGE_SIZE;
  const params = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  const search = request.search?.trim();
  if (search) {
    params.set('search', search);
  }

  return api.get<PagedResult<NoteListItem>>(`/notes?${params.toString()}`);
}

export function createNote(): Promise<CreateNoteResponse> {
  return api.post<CreateNoteResponse>('/notes');
}

export function getNote(noteId: string): Promise<NoteDetails> {
  return api.get<NoteDetails>(`/notes/${noteId}`);
}

export function getNoteStatus(noteId: string): Promise<NoteStatus> {
  return api.get<NoteStatus>(`/notes/${noteId}/status`);
}

export function deleteNote(noteId: string): Promise<unknown> {
  return api.delete(`/notes/${noteId}`);
}

export function uploadNoteAudio(noteId: string, file: RecordingUpload): Promise<NoteStatus> {
  const form = new FormData();
  const uri = file.uri.startsWith('file://') ? file.uri : `file://${file.uri}`;
  form.append('file', {
    uri,
    name: file.name,
    type: file.type,
  } as unknown as Blob);
  form.append('durationSeconds', String(file.durationSeconds));
  return api.upload<NoteStatus>(`/notes/${noteId}/audio`, form);
}

export async function waitForNoteTerminalStatus(
  noteId: string,
  onStatus?: (status: NoteStatus) => void,
): Promise<NoteStatus> {
  for (let attempt = 0; attempt < NOTE_POLL_MAX_ATTEMPTS; attempt += 1) {
    const status = await getNoteStatus(noteId);
    onStatus?.(status);
    if (status.processingStatus === 'Completed' || status.processingStatus === 'Failed') {
      return status;
    }

    await delay(NOTE_POLL_INTERVAL_MS);
  }

  throw new Error('This note is taking longer than expected. Open it from the list in a moment.');
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
