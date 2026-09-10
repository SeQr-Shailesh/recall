import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuthStore } from '@store/authStore';
import { useCustomersStore } from '@store/customersStore';
import { usePendingRecordingsStore } from '@store/pendingRecordingsStore';
import { SyncActionButton } from '@components/SyncActionButton';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import type { Customer } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { CustomersStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<CustomersStackParamList, 'CustomersList'>;

export function CustomersListScreen({ navigation }: Props) {
  const theme = useTheme();
  const signOut = useAuthStore((state) => state.signOut);
  const items = useCustomersStore((state) => state.items);
  const loading = useCustomersStore((state) => state.loading);
  const loadingMore = useCustomersStore((state) => state.loadingMore);
  const loadMore = useCustomersStore((state) => state.loadMore);
  const [searchInput, setSearchInput] = useState('');
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async (query?: string) => {
    try {
      setError(null);
      await usePendingRecordingsStore.getState().hydrate();
      await usePendingRecordingsStore.getState().sync();
      await useCustomersStore.getState().load(query);
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to load customers.'));
    }
  }, []);

  useEffect(() => {
    const handle = setTimeout(() => {
      void refresh(searchInput);
    }, searchInput ? 300 : 0);
    return () => clearTimeout(handle);
  }, [searchInput, refresh]);

  return (
    <SafeAreaView style={[styles.safe, { backgroundColor: theme.colors.background }]}>
      <View style={[styles.header, { paddingHorizontal: theme.spacing.lg }]}>
        <Text style={[theme.typography.title, { color: theme.colors.text, flex: 1 }]}>Customers</Text>
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
        placeholder="Search name, company, mobile, email"
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
            setError(getErrorMessage(caught, 'Unable to load more customers.'));
          });
        }}
        onEndReachedThreshold={0.4}
        ListEmptyComponent={
          loading ? (
            <ActivityIndicator color={theme.colors.primary} style={{ marginTop: 48 }} />
          ) : (
            <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 48 }]}>
              Add a customer to remember conversations later.
            </Text>
          )
        }
        ListFooterComponent={
          loadingMore ? <ActivityIndicator color={theme.colors.primary} style={{ marginVertical: 16 }} /> : undefined
        }
        renderItem={({ item }) => (
          <CustomerRow
            item={item}
            onPress={() => navigation.navigate('CustomerDetails', { customerId: item.id })}
          />
        )}
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Add customer"
        onPress={() => navigation.navigate('CustomerForm', {})}
        style={[styles.add, { backgroundColor: theme.colors.primary }]}
      >
        <Text style={[theme.typography.body, { color: theme.colors.primaryText, fontWeight: '700' }]}>
          Add customer
        </Text>
      </Pressable>
    </SafeAreaView>
  );
}

function CustomerRow({ item, onPress }: { item: Customer; onPress: () => void }) {
  const theme = useTheme();
  const subtitle = [item.companyName, item.mobile, item.email].filter(Boolean).join(' · ');
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
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>{item.name}</Text>
      {subtitle ? (
        <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 4 }]} numberOfLines={2}>
          {subtitle}
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
  add: {
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
