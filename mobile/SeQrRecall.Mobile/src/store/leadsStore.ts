import { create } from 'zustand';
import * as leadsApi from '@api/leadsApi';
import { DEFAULT_PAGE_SIZE } from '@constants/index';
import type { LeadListItem } from '@models/index';

interface LeadsState {
  items: LeadListItem[];
  pageNumber: number;
  totalCount: number;
  totalPages: number;
  search: string;
  loading: boolean;
  loadingMore: boolean;
  load: (search?: string) => Promise<void>;
  loadMore: () => Promise<void>;
  remove: (leadId: string) => void;
}

export const useLeadsStore = create<LeadsState>((set, get) => ({
  items: [],
  pageNumber: 1,
  totalCount: 0,
  totalPages: 0,
  search: '',
  loading: false,
  loadingMore: false,

  load: async (search) => {
    const query = search === undefined ? get().search : search;
    set({ loading: true, search: query, pageNumber: 1 });
    try {
      const page = await leadsApi.listLeads({
        pageNumber: 1,
        pageSize: DEFAULT_PAGE_SIZE,
        search: query || undefined,
      });
      set({
        items: page.items,
        pageNumber: page.pageNumber,
        totalCount: page.totalCount,
        totalPages: page.totalPages,
        loading: false,
      });
    } catch (error) {
      set({ loading: false });
      throw error;
    }
  },

  loadMore: async () => {
    const state = get();
    if (state.loadingMore || state.pageNumber >= state.totalPages) {
      return;
    }

    set({ loadingMore: true });
    try {
      const nextPage = state.pageNumber + 1;
      const page = await leadsApi.listLeads({
        pageNumber: nextPage,
        pageSize: DEFAULT_PAGE_SIZE,
        search: state.search || undefined,
      });
      set({
        items: [...state.items, ...page.items],
        pageNumber: page.pageNumber,
        totalCount: page.totalCount,
        totalPages: page.totalPages,
        loadingMore: false,
      });
    } catch (error) {
      set({ loadingMore: false });
      throw error;
    }
  },

  remove: (leadId) => {
    set((state) => ({
      items: state.items.filter((item) => item.id !== leadId),
      totalCount: Math.max(0, state.totalCount - 1),
    }));
  },
}));
