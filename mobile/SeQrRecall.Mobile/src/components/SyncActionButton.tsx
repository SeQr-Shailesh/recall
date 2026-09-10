import { Pressable, Text } from 'react-native';
import { usePendingRecordingsStore } from '@store/pendingRecordingsStore';
import { useTheme } from '@theme/useTheme';
import { acknowledgeSync } from '@utils/syncAck';

export function SyncActionButton() {
  const theme = useTheme();
  const syncing = usePendingRecordingsStore((state) => state.syncing);
  const pendingCount = usePendingRecordingsStore((state) => state.items.length);

  async function onPress() {
    const result = await usePendingRecordingsStore.getState().sync();
    acknowledgeSync(result);
  }

  const label = syncing ? 'Syncing…' : pendingCount > 0 ? `Sync (${pendingCount})` : 'Sync';

  return (
    <Pressable
      onPress={() => void onPress()}
      disabled={syncing}
      accessibilityRole="button"
      accessibilityLabel="Sync saved recordings"
      accessibilityState={{ busy: syncing, disabled: syncing }}
      hitSlop={8}
    >
      <Text
        style={[
          theme.typography.body,
          { color: theme.colors.primary, fontWeight: '600', opacity: syncing ? 0.6 : 1 },
        ]}
      >
        {label}
      </Text>
    </Pressable>
  );
}
