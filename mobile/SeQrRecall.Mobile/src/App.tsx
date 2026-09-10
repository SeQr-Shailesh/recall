import { useEffect } from 'react';
import { AppState, StatusBar } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { enableScreens } from 'react-native-screens';
import { PENDING_SYNC_INTERVAL_MS } from '@constants/index';
import { RootNavigator } from '@navigation/RootNavigator';
import { canReachApi } from '@services/connectivity';
import { useAuthStore } from '@store/authStore';
import { usePendingRecordingsStore } from '@store/pendingRecordingsStore';
import { useTheme } from '@theme/useTheme';

enableScreens(true);

export function App() {
  const theme = useTheme();
  const hydrate = useAuthStore((state) => state.hydrate);
  const isReady = useAuthStore((state) => state.isReady);
  const user = useAuthStore((state) => state.user);
  const hydratePending = usePendingRecordingsStore((state) => state.hydrate);
  const syncPending = usePendingRecordingsStore((state) => state.sync);

  useEffect(() => {
    void hydrate();
  }, [hydrate]);

  useEffect(() => {
    void hydratePending();
  }, [hydratePending]);

  useEffect(() => {
    if (!isReady || !user) {
      return;
    }

    void syncPending();
    const subscription = AppState.addEventListener('change', (nextState) => {
      if (nextState === 'active') {
        void syncPending();
      }
    });
    const interval = setInterval(() => {
      const pending = usePendingRecordingsStore.getState();
      if (pending.syncing || pending.items.length === 0) {
        return;
      }

      void (async () => {
        if (await canReachApi()) {
          await syncPending();
        }
      })();
    }, PENDING_SYNC_INTERVAL_MS);
    return () => {
      subscription.remove();
      clearInterval(interval);
    };
  }, [isReady, syncPending, user]);

  return (
    <SafeAreaProvider>
      <StatusBar
        barStyle={theme.colors.background === '#121417' ? 'light-content' : 'dark-content'}
      />
      <RootNavigator />
    </SafeAreaProvider>
  );
}
