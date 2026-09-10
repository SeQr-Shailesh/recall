import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Image,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import * as leadsApi from '@api/leadsApi';
import { SyncActionButton } from '@components/SyncActionButton';
import type { LocalRecording } from '@services/localRecordings';
import { choosePhoto } from '@services/photoPicker';
import { useLeadsStore } from '@store/leadsStore';
import {
  pendingLeads,
  pendingStatusCopy,
  usePendingRecordingsStore,
} from '@store/pendingRecordingsStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage, OFFLINE_MESSAGE } from '@utils/errors';
import { acknowledgeRetry } from '@utils/syncAck';
import { leadStatusCopy, leadTitle } from '@utils/leadStatus';
import type { LeadListItem } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { LeadsStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<LeadsStackParamList, 'LeadsList'>;

export function LeadsListScreen({ navigation }: Props) {
  const theme = useTheme();
  const items = useLeadsStore((state) => state.items);
  const loading = useLeadsStore((state) => state.loading);
  const loadingMore = useLeadsStore((state) => state.loadingMore);
  const loadMore = useLeadsStore((state) => state.loadMore);
  const recordings = usePendingRecordingsStore((state) => state.items);
  const pendingItems = useMemo(() => pendingLeads(recordings), [recordings]);
  const retryPending = usePendingRecordingsStore((state) => state.retry);
  const discardPending = usePendingRecordingsStore((state) => state.discard);
  const attachPhoto = usePendingRecordingsStore((state) => state.attachPhoto);
  const [searchInput, setSearchInput] = useState('');
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async (query?: string) => {
    try {
      setError(null);
      await usePendingRecordingsStore.getState().hydrate();
      await usePendingRecordingsStore.getState().sync();
      await useLeadsStore.getState().load(query);
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to load leads.'));
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
          text: recording.photoPath ? 'Change photo' : 'Add photo',
          onPress: () => {
            void (async () => {
              try {
                const picked = await choosePhoto('Lead photo');
                if (picked) {
                  await attachPhoto(recording.id, picked);
                }
              } catch (caught) {
                setError(getErrorMessage(caught, 'Unable to add a photo.'));
              }
            })();
          },
        },
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
        <Text style={[theme.typography.title, { color: theme.colors.text, flex: 1 }]}>Leads</Text>
        <SyncActionButton />
      </View>
      <TextInput
        value={searchInput}
        onChangeText={setSearchInput}
        placeholder="Search leads"
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
            setError(getErrorMessage(caught, 'Unable to load more leads.'));
          });
        }}
        onEndReachedThreshold={0.4}
        ListHeaderComponent={
          pendingItems.length > 0 ? (
            <View>
              {pendingItems.map((item) => (
                <PendingLeadRow key={item.id} item={item} onPress={() => onPendingPress(item)} />
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
              Add a lead by speaking. You can attach a photo too.
            </Text>
          )
        }
        ListFooterComponent={
          loadingMore ? <ActivityIndicator color={theme.colors.primary} style={{ marginVertical: 16 }} /> : undefined
        }
        renderItem={({ item }) => (
          <LeadRow item={item} onPress={() => navigation.navigate('LeadDetails', { leadId: item.id })} />
        )}
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Add lead"
        onPress={() => navigation.navigate('RecordLead')}
        style={[styles.speak, { backgroundColor: theme.colors.primary }]}
      >
        <Text style={[theme.typography.body, { color: theme.colors.primaryText, fontWeight: '700' }]}>
          Add lead
        </Text>
      </Pressable>
    </SafeAreaView>
  );
}

function PendingLeadRow({ item, onPress }: { item: LocalRecording; onPress: () => void }) {
  const theme = useTheme();
  const created = new Date(item.createdOn);
  return (
    <Pressable
      onPress={onPress}
      style={[styles.row, { backgroundColor: theme.colors.surface, borderColor: theme.colors.border }]}
    >
      <View style={styles.rowBody}>
        {item.photoPath ? (
          <Image
            source={{ uri: `file://${item.photoPath}` }}
            style={[styles.thumbnail, { backgroundColor: theme.colors.border }]}
          />
        ) : null}
        <View style={styles.rowText}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>New lead</Text>
          <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]}>
            {pendingStatusCopy(item)}
            {Number.isNaN(created.getTime()) ? '' : ` · ${created.toLocaleString()}`}
          </Text>
        </View>
      </View>
    </Pressable>
  );
}

function LeadRow({ item, onPress }: { item: LeadListItem; onPress: () => void }) {
  const theme = useTheme();
  const created = new Date(item.createdOn);
  const [photoUri, setPhotoUri] = useState<string | null>(null);

  useEffect(() => {
    if (!item.hasPhoto) {
      setPhotoUri(null);
      return;
    }

    let active = true;
    void leadsApi
      .getLeadPhotoDataUri(item.id)
      .then((uri) => {
        if (active) {
          setPhotoUri(uri);
        }
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [item.hasPhoto, item.id]);

  return (
    <Pressable
      onPress={onPress}
      style={[styles.row, { backgroundColor: theme.colors.surface, borderColor: theme.colors.border }]}
    >
      <View style={styles.rowBody}>
        {photoUri ? (
          <Image
            source={{ uri: photoUri }}
            style={[styles.thumbnail, { backgroundColor: theme.colors.border }]}
          />
        ) : item.hasPhoto ? (
          <View style={[styles.thumbnail, { backgroundColor: theme.colors.border }]} />
        ) : null}
        <View style={styles.rowText}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>{leadTitle(item.title)}</Text>
          <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]}>
            {leadStatusCopy(item.processingStatus)}
            {Number.isNaN(created.getTime()) ? '' : ` · ${created.toLocaleString()}`}
          </Text>
          {item.shortSummary ? (
            <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]} numberOfLines={2}>
              {item.shortSummary}
            </Text>
          ) : null}
        </View>
      </View>
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
  rowBody: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 12,
  },
  rowText: {
    flex: 1,
  },
  thumbnail: {
    width: 56,
    height: 56,
    borderRadius: 8,
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
