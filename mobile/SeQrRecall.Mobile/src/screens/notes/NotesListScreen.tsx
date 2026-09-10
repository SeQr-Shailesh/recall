import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { APP_NAME } from '@constants/index';
import { SyncActionButton } from '@components/SyncActionButton';
import type { LocalRecording } from '@services/localRecordings';
import { useAuthStore } from '@store/authStore';
import { useNotesStore } from '@store/notesStore';
import {
  pendingNotes,
  pendingStatusCopy,
  usePendingRecordingsStore,
} from '@store/pendingRecordingsStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage, OFFLINE_MESSAGE } from '@utils/errors';
import { acknowledgeRetry } from '@utils/syncAck';
import { noteStatusCopy, noteTitle } from '@utils/noteStatus';
import type { NoteListItem } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<AppStackParamList, 'NotesList'>;

export function NotesListScreen({ navigation }: Props) {
  const theme = useTheme();
  const signOut = useAuthStore((state) => state.signOut);
  const items = useNotesStore((state) => state.items);
  const loading = useNotesStore((state) => state.loading);
  const loadingMore = useNotesStore((state) => state.loadingMore);
  const loadMore = useNotesStore((state) => state.loadMore);
  const recordings = usePendingRecordingsStore((state) => state.items);
  const pendingItems = useMemo(() => pendingNotes(recordings), [recordings]);
  const retryPending = usePendingRecordingsStore((state) => state.retry);
  const discardPending = usePendingRecordingsStore((state) => state.discard);
  const [searchInput, setSearchInput] = useState('');
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async (query?: string) => {
    try {
      setError(null);
      await usePendingRecordingsStore.getState().hydrate();
      await usePendingRecordingsStore.getState().sync();
      await useNotesStore.getState().load(query);
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to load notes.'));
    }
  }, []);

  useEffect(() => {
    const handle = setTimeout(() => {
      void refresh(searchInput);
    }, searchInput ? 300 : 0);
    return () => clearTimeout(handle);
  }, [refresh, searchInput]);

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      void refresh(searchInput);
    });
    return unsubscribe;
  }, [navigation, refresh, searchInput]);

  function onPendingPress(recording: LocalRecording) {
    Alert.alert(
      'Saved on this phone',
      OFFLINE_MESSAGE,
      [
        { text: 'OK' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: () => {
            void discardPending(recording.id);
          },
        },
        {
          text: 'Send now',
          onPress: () => {
            void (async () => {
              const result = await retryPending(recording.id);
              acknowledgeRetry(result);
              if (result.status === 'uploaded') {
                await refresh(searchInput);
              }
            })();
          },
        },
      ],
    );
  }

  return (
    <SafeAreaView style={[styles.safe, { backgroundColor: theme.colors.background }]}>
      <View style={[styles.header, { paddingHorizontal: theme.spacing.lg }]}>
        <Text style={[theme.typography.title, { color: theme.colors.text, flex: 1 }]}>{APP_NAME}</Text>
        <View style={styles.headerActions}>
          <SyncActionButton />
          <Pressable onPress={() => void signOut()} accessibilityRole="button">
            <Text style={[theme.typography.body, { color: theme.colors.primary, fontWeight: '600' }]}>
              Sign out
            </Text>
          </Pressable>
        </View>
      </View>
      <TextInput
        value={searchInput}
        onChangeText={setSearchInput}
        placeholder="Search notes"
        placeholderTextColor={theme.colors.mutedText}
        autoCapitalize="none"
        style={[
          styles.search,
          theme.typography.body,
          {
            marginHorizontal: theme.spacing.lg,
            color: theme.colors.text,
            backgroundColor: theme.colors.surface,
            borderColor: theme.colors.border,
          },
        ]}
      />
      {error ? (
        <Text
          style={[
            theme.typography.caption,
            { color: theme.colors.danger, marginHorizontal: theme.spacing.lg, marginBottom: 8 },
          ]}
        >
          {error}
        </Text>
      ) : null}
      <FlatList
        data={items}
        keyExtractor={(item) => item.id}
        contentContainerStyle={{ paddingHorizontal: theme.spacing.lg, paddingBottom: 120 }}
        refreshControl={
          <RefreshControl refreshing={loading && items.length > 0} onRefresh={() => void refresh(searchInput)} />
        }
        onEndReached={() => {
          void loadMore().catch((caught: unknown) => {
            setError(getErrorMessage(caught, 'Unable to load more notes.'));
          });
        }}
        onEndReachedThreshold={0.4}
        ListHeaderComponent={
          pendingItems.length > 0 ? (
            <View>
              {pendingItems.map((item) => (
                <PendingNoteRow key={item.id} item={item} onPress={() => onPendingPress(item)} />
              ))}
            </View>
          ) : (
            <></>
          )
        }
        ListEmptyComponent={
          loading ? (
            <ActivityIndicator color={theme.colors.primary} style={{ marginTop: 48 }} />
          ) : pendingItems.length > 0 ? (
            <></>
          ) : (
            <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 48 }]}>
              Tap to speak a note. It will show up here.
            </Text>
          )
        }
        ListFooterComponent={
          loadingMore ? <ActivityIndicator color={theme.colors.primary} style={{ marginVertical: 16 }} /> : undefined
        }
        renderItem={({ item }) => (
          <NoteRow
            item={item}
            onPress={() => navigation.navigate('NoteDetails', { noteId: item.id })}
          />
        )}
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Tap to speak"
        onPress={() => navigation.navigate('Record')}
        style={[styles.speak, { backgroundColor: theme.colors.primary }]}
      >
        <Text style={[theme.typography.body, { color: theme.colors.primaryText, fontWeight: '700' }]}>
          Tap to speak
        </Text>
      </Pressable>
    </SafeAreaView>
  );
}

function PendingNoteRow({ item, onPress }: { item: LocalRecording; onPress: () => void }) {
  const theme = useTheme();
  const created = new Date(item.createdOn);
  return (
    <Pressable
      onPress={onPress}
      style={[
        styles.row,
        {
          backgroundColor: theme.colors.surface,
          borderColor: theme.colors.border,
        },
      ]}
    >
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Voice note</Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]}>
        {pendingStatusCopy(item)}
        {Number.isNaN(created.getTime()) ? '' : ` · ${created.toLocaleString()}`}
      </Text>
    </Pressable>
  );
}

function NoteRow({ item, onPress }: { item: NoteListItem; onPress: () => void }) {
  const theme = useTheme();
  const created = new Date(item.createdOn);
  return (
    <Pressable
      onPress={onPress}
      style={[
        styles.row,
        {
          backgroundColor: theme.colors.surface,
          borderColor: theme.colors.border,
        },
      ]}
    >
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>{noteTitle(item.title)}</Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]}>
        {noteStatusCopy(item.processingStatus)}
        {Number.isNaN(created.getTime()) ? '' : ` · ${created.toLocaleString()}`}
      </Text>
      {item.shortSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]} numberOfLines={2}>
          {item.shortSummary}
        </Text>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingTop: 8,
    paddingBottom: 12,
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 16,
  },
  search: {
    borderWidth: 1,
    borderRadius: 12,
    minHeight: 44,
    paddingHorizontal: 14,
    marginBottom: 12,
  },
  row: {
    borderWidth: 1,
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
  },
  speak: {
    position: 'absolute',
    right: 24,
    bottom: 24,
    minHeight: 56,
    paddingHorizontal: 20,
    borderRadius: 28,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
