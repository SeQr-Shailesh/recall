import { create } from 'zustand';
import * as customersApi from '@api/customersApi';
import { DEFAULT_PAGE_SIZE } from '@constants/index';
import type { Customer } from '@models/index';

interface CustomersState {
  items: Customer[];
  pageNumber: number;
  totalCount: number;
  totalPages: number;
  search: string;
  loading: boolean;
  loadingMore: boolean;
  load: (search?: string) => Promise<void>;
  loadMore: () => Promise<void>;
  upsert: (customer: Customer) => void;
  remove: (customerId: string) => void;
}

export const useCustomersStore = create<CustomersState>((set, get) => ({
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
      const page = await customersApi.listCustomers({
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
      const page = await customersApi.listCustomers({
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

  upsert: (customer) => {
    set((state) => {
      const existing = state.items.findIndex((item) => item.id === customer.id);
      if (existing >= 0) {
        const items = [...state.items];
        items[existing] = customer;
        return { items };
      }

      return {
        items: [customer, ...state.items],
        totalCount: state.totalCount + 1,
      };
    });
  },

  remove: (customerId) => {
    set((state) => ({
      items: state.items.filter((item) => item.id !== customerId),
      totalCount: Math.max(0, state.totalCount - 1),
    }));
  },
}));
