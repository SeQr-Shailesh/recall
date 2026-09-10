import { create } from 'zustand';
import * as authApi from '@api/authApi';
import { clearTokens, loadTokens, saveTokens } from '@services/secureStorage';
import { clearUserSnapshot, loadUserSnapshot, saveUserSnapshot } from '@services/sessionCache';
import { tokenSession } from '@services/tokenSession';
import { isLikelyOfflineError } from '@utils/errors';
import type { User } from '@models/index';
import type { AuthIdentifier } from '@utils/identifier';

interface AuthState {
  isReady: boolean;
  user: User | null;
  identifier: AuthIdentifier | null;
  hydrate: () => Promise<void>;
  setIdentifier: (identifier: AuthIdentifier) => void;
  sendOtp: (identifier: AuthIdentifier) => Promise<void>;
  verifyOtp: (otp: string) => Promise<{ needsProfile: boolean }>;
  completeProfile: (fullName: string) => Promise<void>;
  signOut: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  isReady: false,
  user: null,
  identifier: null,

  hydrate: async () => {
    const tokens = await loadTokens();
    if (!tokens) {
      tokenSession.clear();
      await clearUserSnapshot();
      set({ isReady: true, user: null });
      return;
    }

    tokenSession.setAccessToken(tokens.accessToken);
    try {
      const user = await authApi.getCurrentUser();
      await saveUserSnapshot(user);
      set({ isReady: true, user });
    } catch (error) {
      if (isLikelyOfflineError(error)) {
        const cached = await loadUserSnapshot();
        if (cached) {
          set({ isReady: true, user: cached });
          return;
        }
      }

      tokenSession.clear();
      await clearTokens();
      await clearUserSnapshot();
      set({ isReady: true, user: null });
    }
  },

  setIdentifier: (identifier) => {
    set({ identifier });
  },

  sendOtp: async (identifier) => {
    set({ identifier });
    await authApi.sendOtp(identifier);
  },

  verifyOtp: async (otp) => {
    const identifier = get().identifier;
    if (!identifier) {
      throw new Error('Enter a mobile number or email first.');
    }

    const result = await authApi.verifyOtp(identifier, otp);
    tokenSession.setAccessToken(result.tokens.accessToken);
    await saveTokens(result.tokens);
    await saveUserSnapshot(result.user);
    set({ user: result.user });
    return { needsProfile: result.isNewUser || !result.user.isProfileComplete };
  },

  completeProfile: async (fullName) => {
    const current = get().user;
    const user = await authApi.updateProfile({
      fullName,
      mobile: current?.mobile,
      email: current?.email,
    });
    await saveUserSnapshot(user);
    set({ user });
  },

  signOut: async () => {
    const tokens = await loadTokens();
    try {
      if (tokens?.refreshToken) {
        await authApi.logout(tokens.refreshToken);
      }
    } catch {
      // Local sign-out still proceeds if the network call fails.
    }

    tokenSession.clear();
    await clearTokens();
    await clearUserSnapshot();
    set({ user: null, identifier: null });
  },
}));
