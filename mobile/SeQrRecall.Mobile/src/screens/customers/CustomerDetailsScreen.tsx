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
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { PrimaryButton } from '@components/PrimaryButton';
import { SyncActionButton } from '@components/SyncActionButton';
import * as customersApi from '@api/customersApi';
import type { LocalRecording } from '@services/localRecordings';
import { useCustomersStore } from '@store/customersStore';
import { useInteractionsStore } from '@store/interactionsStore';
import {
  pendingInteractionsForCustomer,
  pendingStatusCopy,
  usePendingRecordingsStore,
} from '@store/pendingRecordingsStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage, OFFLINE_MESSAGE } from '@utils/errors';
import { acknowledgeRetry } from '@utils/syncAck';
import { interactionStatusCopy, interactionTitle } from '@utils/interactionStatus';
import type { Customer, CustomerInteractionListItem } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { CustomersStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<CustomersStackParamList, 'CustomerDetails'>;

export function CustomerDetailsScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const remove = useCustomersStore((state) => state.remove);
  const items = useInteractionsStore((state) => state.items);
  const loadingMore = useInteractionsStore((state) => state.loadingMore);
  const interactionsLoading = useInteractionsStore((state) => state.loading);
  const loadInteractions = useInteractionsStore((state) => state.load);
  const loadMoreInteractions = useInteractionsStore((state) => state.loadMore);
  const recordings = usePendingRecordingsStore((state) => state.items);
  const pendingItems = useMemo(
    () => pendingInteractionsForCustomer(recordings, route.params.customerId),
    [recordings, route.params.customerId],
  );
  const hydratePending = usePendingRecordingsStore((state) => state.hydrate);
  const syncPending = usePendingRecordingsStore((state) => state.sync);
  const retryPending = usePendingRecordingsStore((state) => state.retry);
  const discardPending = usePendingRecordingsStore((state) => state.discard);
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [photoUri, setPhotoUri] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      setError(null);
      const details = await customersApi.getCustomer(route.params.customerId);
      setCustomer(details);
      if (details.hasPhoto) {
        const uri = await customersApi.getCustomerPhotoDataUri(details.id);
        setPhotoUri(uri);
      } else {
        setPhotoUri(null);
      }
      await hydratePending();
      await syncPending();
      await loadInteractions(details.id);
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to open this customer.'));
    } finally {
      setLoading(false);
    }
  }, [hydratePending, loadInteractions, route.params.customerId, syncPending]);

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      void load();
    });
    return unsubscribe;
  }, [load, navigation]);

  function onDelete() {
    Alert.alert('Delete customer', 'This customer will be removed.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: () => {
          void (async () => {
            try {
              await customersApi.deleteCustomer(route.params.customerId);
              remove(route.params.customerId);
              navigation.goBack();
            } catch (caught) {
              setError(getErrorMessage(caught, 'Unable to delete this customer.'));
            }
          })();
        },
      },
    ]);
  }

  if (loading && !customer) {
    return (
      <SafeAreaView style={[styles.safe, { backgroundColor: theme.colors.background }]}>
        <ActivityIndicator color={theme.colors.primary} style={{ marginTop: 48 }} />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={[styles.safe, { backgroundColor: theme.colors.background }]}>
      <FlatList
        data={items}
        keyExtractor={(item) => item.id}
        contentContainerStyle={{ paddingHorizontal: theme.spacing.lg, paddingBottom: 120 }}
        refreshControl={
          <RefreshControl refreshing={interactionsLoading && items.length > 0} onRefresh={() => void load()} />
        }
        onEndReached={() => {
          void loadMoreInteractions().catch((caught: unknown) => {
            setError(getErrorMessage(caught, 'Unable to load more conversations.'));
          });
        }}
        onEndReachedThreshold={0.4}
        ListHeaderComponent={
          <View>
            <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <Pressable onPress={() => navigation.goBack()}>
                <Text style={[theme.typography.body, { color: theme.colors.primary, fontWeight: '600' }]}>
                  Back
                </Text>
              </Pressable>
              <SyncActionButton />
            </View>
            {photoUri ? (
              <Image
                source={{ uri: photoUri }}
                style={{
                  width: 96,
                  height: 96,
                  borderRadius: 12,
                  marginTop: 16,
                  backgroundColor: theme.colors.border,
                }}
              />
            ) : null}
            <Text style={[theme.typography.title, { color: theme.colors.text, marginTop: 12 }]}>
              {customer?.name ?? 'Customer'}
            </Text>
            {customer?.companyName ? (
              <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]}>
                {customer.companyName}
              </Text>
            ) : null}
            {customer?.mobile ? (
              <Text style={[theme.typography.body, { color: theme.colors.text, marginTop: 8 }]}>
                {customer.mobile}
              </Text>
            ) : null}
            {customer?.email ? (
              <Text style={[theme.typography.body, { color: theme.colors.text, marginTop: 8 }]}>
                {customer.email}
              </Text>
            ) : null}
            {error ? (
              <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>{error}</Text>
            ) : null}
            <View style={{ marginTop: 20, marginBottom: 8, gap: 12 }}>
              <PrimaryButton
                label="Edit customer"
                onPress={() => navigation.navigate('CustomerForm', { customerId: route.params.customerId })}
              />
              <PrimaryButton label="Delete customer" onPress={onDelete} />
            </View>
            <Text style={[theme.typography.heading, { color: theme.colors.text, marginTop: 16, marginBottom: 8 }]}>
              Conversations
            </Text>
            {pendingItems.map((item) => (
              <PendingInteractionRow
                key={item.id}
                item={item}
                onPress={() => {
                  Alert.alert(
                    'Saved on this phone',
                    OFFLINE_MESSAGE,
                    [
                      { text: 'OK' },
                      {
                        text: 'Delete',
                        style: 'destructive',
                        onPress: () => {
                          void discardPending(item.id);
                        },
                      },
                      {
                        text: 'Send now',
                        onPress: () => {
                            void (async () => {
                              const result = await retryPending(item.id);
                              acknowledgeRetry(result);
                              if (result.status === 'uploaded') {
                                await load();
                              }
                            })();
                        },
                      },
                    ],
                  );
                }}
              />
            ))}
          </View>
        }
        ListEmptyComponent={
          interactionsLoading ? (
            <ActivityIndicator color={theme.colors.primary} style={{ marginTop: 24 }} />
          ) : pendingItems.length > 0 ? (
            <></>
          ) : (
            <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 12 }]}>
              Tap to speak a conversation with this customer.
            </Text>
          )
        }
        ListFooterComponent={
          loadingMore ? <ActivityIndicator color={theme.colors.primary} style={{ marginVertical: 16 }} /> : undefined
        }
        renderItem={({ item }) => (
          <InteractionRow
            item={item}
            onPress={() => navigation.navigate('InteractionDetails', { interactionId: item.id })}
          />
        )}
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Tap to speak"
        onPress={() => navigation.navigate('RecordInteraction', { customerId: route.params.customerId })}
        style={[styles.speak, { backgroundColor: theme.colors.primary }]}
      >
        <Text style={[theme.typography.body, { color: theme.colors.primaryText, fontWeight: '700' }]}>
          Tap to speak
        </Text>
      </Pressable>
    </SafeAreaView>
  );
}

function PendingInteractionRow({ item, onPress }: { item: LocalRecording; onPress: () => void }) {
  const theme = useTheme();
  const when = new Date(item.createdOn);
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
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Conversation</Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]}>
        {pendingStatusCopy(item)}
        {Number.isNaN(when.getTime()) ? '' : ` · ${when.toLocaleString()}`}
      </Text>
    </Pressable>
  );
}

function InteractionRow({
  item,
  onPress,
}: {
  item: CustomerInteractionListItem;
  onPress: () => void;
}) {
  const theme = useTheme();
  const when = new Date(item.interactionDate);
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
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>
        {interactionTitle(item.shortSummary)}
      </Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]}>
        {interactionStatusCopy(item.processingStatus)}
        {Number.isNaN(when.getTime()) ? '' : ` · ${when.toLocaleString()}`}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
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
