import { api } from '@api/client';
import {
  DEFAULT_PAGE_SIZE,
  NOTE_POLL_INTERVAL_MS,
  NOTE_POLL_MAX_ATTEMPTS,
} from '@constants/index';
import type { PhotoUpload } from '@api/customersApi';
import type { RecordingUpload } from '@api/notesApi';
import type {
  CreateLeadResponse,
  LeadDetails,
  LeadListItem,
  NoteStatus,
  PagedRequest,
  PagedResult,
} from '@models/index';

export function listLeads(request: PagedRequest = {}): Promise<PagedResult<LeadListItem>> {
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

  return api.get<PagedResult<LeadListItem>>(`/leads?${params.toString()}`);
}

export function createLead(): Promise<CreateLeadResponse> {
  return api.post<CreateLeadResponse>('/leads');
}

export function getLead(leadId: string): Promise<LeadDetails> {
  return api.get<LeadDetails>(`/leads/${leadId}`);
}

export function getLeadStatus(leadId: string): Promise<NoteStatus> {
  return api.get<NoteStatus>(`/leads/${leadId}/status`);
}

export function deleteLead(leadId: string): Promise<unknown> {
  return api.delete(`/leads/${leadId}`);
}

export function uploadLeadAudio(leadId: string, file: RecordingUpload): Promise<NoteStatus> {
  const form = new FormData();
  const uri = file.uri.startsWith('file://') ? file.uri : `file://${file.uri}`;
  form.append('file', {
    uri,
    name: file.name,
    type: file.type,
  } as unknown as Blob);
  form.append('durationSeconds', String(file.durationSeconds));
  return api.upload<NoteStatus>(`/leads/${leadId}/audio`, form);
}

export async function uploadLeadPhoto(leadId: string, file: PhotoUpload): Promise<LeadDetails> {
  const form = new FormData();
  const uri = file.uri.startsWith('file://') ? file.uri : `file://${file.uri}`;
  form.append('file', {
    uri,
    name: file.name,
    type: file.type,
  } as unknown as Blob);
  const lead = await api.upload<LeadDetails>(`/leads/${leadId}/photo`, form);
  photoCache.delete(leadId);
  return lead;
}

/**
 * Photos come back as binary and are turned into data URIs, so list thumbnails are cached
 * to keep scrolling from re-downloading the same image. Base64 photos can be a few MB each,
 * so the cache is capped and evicts least-recently-used entries.
 */
const photoCache = new Map<string, string>();
const PHOTO_CACHE_LIMIT = 40;

export async function getLeadPhotoDataUri(leadId: string): Promise<string> {
  const cached = photoCache.get(leadId);
  if (cached) {
    photoCache.delete(leadId);
    photoCache.set(leadId, cached);
    return cached;
  }

  const binary = await api.getBinary(`/leads/${leadId}/photo`);
  const uri = `data:${binary.contentType};base64,${binary.base64}`;
  photoCache.set(leadId, uri);
  while (photoCache.size > PHOTO_CACHE_LIMIT) {
    const oldest = photoCache.keys().next();
    if (oldest.done) {
      break;
    }

    photoCache.delete(oldest.value);
  }

  return uri;
}

export async function waitForLeadTerminalStatus(
  leadId: string,
  onStatus?: (status: NoteStatus) => void,
): Promise<NoteStatus> {
  for (let attempt = 0; attempt < NOTE_POLL_MAX_ATTEMPTS; attempt += 1) {
    const status = await getLeadStatus(leadId);
    onStatus?.(status);
    if (status.processingStatus === 'Completed' || status.processingStatus === 'Failed') {
      return status;
    }

    await delay(NOTE_POLL_INTERVAL_MS);
  }

  throw new Error('This lead is taking longer than expected. Open it from the list in a moment.');
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
