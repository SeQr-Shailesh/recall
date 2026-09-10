import { api } from '@api/client';
import {
  DEFAULT_PAGE_SIZE,
  NOTE_POLL_INTERVAL_MS,
  NOTE_POLL_MAX_ATTEMPTS,
} from '@constants/index';
import type { PhotoUpload } from '@api/customersApi';
import type { RecordingUpload } from '@api/notesApi';
import type {
  CreateCustomerInteractionResponse,
  CustomerInteraction,
  CustomerInteractionListItem,
  NoteStatus,
  PagedRequest,
  PagedResult,
} from '@models/index';

export function listInteractions(
  customerId: string,
  request: PagedRequest = {},
): Promise<PagedResult<CustomerInteractionListItem>> {
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

  return api.get<PagedResult<CustomerInteractionListItem>>(
    `/customers/${customerId}/interactions?${params.toString()}`,
  );
}

export function createInteraction(customerId: string): Promise<CreateCustomerInteractionResponse> {
  return api.post<CreateCustomerInteractionResponse>('/customer-interactions', { customerId });
}

export function getInteraction(interactionId: string): Promise<CustomerInteraction> {
  return api.get<CustomerInteraction>(`/customer-interactions/${interactionId}`);
}

export function getInteractionStatus(interactionId: string): Promise<NoteStatus> {
  return api.get<NoteStatus>(`/customer-interactions/${interactionId}/status`);
}

export function uploadInteractionAudio(interactionId: string, file: RecordingUpload): Promise<NoteStatus> {
  const form = new FormData();
  const uri = file.uri.startsWith('file://') ? file.uri : `file://${file.uri}`;
  form.append('file', {
    uri,
    name: file.name,
    type: file.type,
  } as unknown as Blob);
  form.append('durationSeconds', String(file.durationSeconds));
  return api.upload<NoteStatus>(`/customer-interactions/${interactionId}/audio`, form);
}

export function uploadInteractionPhoto(
  interactionId: string,
  file: PhotoUpload,
): Promise<CustomerInteraction> {
  const form = new FormData();
  const uri = file.uri.startsWith('file://') ? file.uri : `file://${file.uri}`;
  form.append('file', {
    uri,
    name: file.name,
    type: file.type,
  } as unknown as Blob);
  return api.upload<CustomerInteraction>(`/customer-interactions/${interactionId}/photo`, form);
}

export async function getInteractionPhotoDataUri(interactionId: string): Promise<string> {
  const binary = await api.getBinary(`/customer-interactions/${interactionId}/photo`);
  return `data:${binary.contentType};base64,${binary.base64}`;
}

export async function waitForInteractionTerminalStatus(
  interactionId: string,
  onStatus?: (status: NoteStatus) => void,
): Promise<NoteStatus> {
  for (let attempt = 0; attempt < NOTE_POLL_MAX_ATTEMPTS; attempt += 1) {
    const status = await getInteractionStatus(interactionId);
    onStatus?.(status);
    if (status.processingStatus === 'Completed' || status.processingStatus === 'Failed') {
      return status;
    }

    await delay(NOTE_POLL_INTERVAL_MS);
  }

  throw new Error('This conversation is taking longer than expected. Open it from the list in a moment.');
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
