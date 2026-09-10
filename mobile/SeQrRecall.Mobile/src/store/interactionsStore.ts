import { create } from 'zustand';
import * as interactionsApi from '@api/interactionsApi';
import { DEFAULT_PAGE_SIZE } from '@constants/index';
import type { CustomerInteractionListItem } from '@models/index';

interface InteractionsState {
  customerId: string | null;
  items: CustomerInteractionListItem[];
  pageNumber: number;
  totalCount: number;
  totalPages: number;
  search: string;
  loading: boolean;
  loadingMore: boolean;
  load: (customerId: string, search?: string) => Promise<void>;
  loadMore: () => Promise<void>;
}

export const useInteractionsStore = create<InteractionsState>((set, get) => ({
  customerId: null,
  items: [],
  pageNumber: 1,
  totalCount: 0,
  totalPages: 0,
  search: '',
  loading: false,
  loadingMore: false,

  load: async (customerId, search) => {
    const query = search === undefined ? get().search : search;
    set({ loading: true, customerId, search: query, pageNumber: 1 });
    try {
      const page = await interactionsApi.listInteractions(customerId, {
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
    if (!state.customerId || state.loadingMore || state.pageNumber >= state.totalPages) {
      return;
    }

    set({ loadingMore: true });
    try {
      const nextPage = state.pageNumber + 1;
      const page = await interactionsApi.listInteractions(state.customerId, {
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
}));
