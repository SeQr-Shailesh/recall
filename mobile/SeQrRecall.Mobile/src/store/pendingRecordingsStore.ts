import { create } from 'zustand';
import {
  attachLocalPhoto,
  enqueueLocalRecording,
  listLocalRecordings,
  removeLocalRecording,
  type LocalPhoto,
  type LocalRecording,
  type LocalRecordingMeta,
} from '@services/localRecordings';
import { processLocalRecording, syncPendingRecordings, type ProcessRecordingResult, type SyncPendingResult } from '@services/recordingSync';
import type { FinishedRecording } from '@services/audioRecorder';

interface PendingRecordingsState {
  items: LocalRecording[];
  syncing: boolean;
  hydrate: () => Promise<void>;
  keep: (recording: FinishedRecording, meta: LocalRecordingMeta) => Promise<LocalRecording>;
  attachPhoto: (id: string, photo: LocalPhoto) => Promise<void>;
  sync: () => Promise<SyncPendingResult>;
  retry: (id: string) => Promise<ProcessRecordingResult>;
  discard: (id: string) => Promise<void>;
}

export const usePendingRecordingsStore = create<PendingRecordingsState>((set, get) => ({
  items: [],
  syncing: false,

  hydrate: async () => {
    const items = await listLocalRecordings();
    set({ items });
  },

  keep: async (recording, meta) => {
    const item = await enqueueLocalRecording(recording, meta);
    set((state) => ({
      items: [item, ...state.items.filter((existing) => existing.id !== item.id)],
    }));
    return item;
  },

  attachPhoto: async (id, photo) => {
    const updated = await attachLocalPhoto(id, photo);
    if (!updated) {
      return;
    }

    set((state) => ({
      items: state.items.map((item) => (item.id === id ? updated : item)),
    }));
  },

  sync: async () => {
    const empty: SyncPendingResult = {
      attempted: 0,
      uploaded: 0,
      remaining: get().items.length,
      offline: false,
      lastError: null,
    };

    if (get().syncing) {
      return empty;
    }

    set({ syncing: true });
    try {
      const result = await syncPendingRecordings();
      await get().hydrate();
      return { ...result, remaining: get().items.length };
    } finally {
      set({ syncing: false });
    }
  },

  retry: async (id) => {
    const result = await processLocalRecording(id);
    await get().hydrate();
    return result;
  },

  discard: async (id) => {
    await removeLocalRecording(id);
    set((state) => ({ items: state.items.filter((item) => item.id !== id) }));
  },
}));

export function pendingNotes(items: LocalRecording[]): LocalRecording[] {
  return items.filter((item) => item.kind === 'note');
}

export function pendingLeads(items: LocalRecording[]): LocalRecording[] {
  return items.filter((item) => item.kind === 'lead');
}

export function pendingInteractionsForCustomer(
  items: LocalRecording[],
  customerId: string,
): LocalRecording[] {
  return items.filter((item) => item.kind === 'interaction' && item.customerId === customerId);
}

export function pendingStatusCopy(item?: LocalRecording): string {
  if (item?.lastError) {
    return item.lastError;
  }

  return 'Saved on this phone · will sync when the network is back';
}
