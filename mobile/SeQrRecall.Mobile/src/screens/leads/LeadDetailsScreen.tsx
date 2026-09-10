import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Image, Pressable, Text, View } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import * as leadsApi from '@api/leadsApi';
import { choosePhoto } from '@services/photoPicker';
import { useLeadsStore } from '@store/leadsStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import { isLeadBusy, leadStatusCopy, leadTitle } from '@utils/leadStatus';
import type { LeadDetails } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { LeadsStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<LeadsStackParamList, 'LeadDetails'>;

export function LeadDetailsScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const remove = useLeadsStore((state) => state.remove);
  const loadLeads = useLeadsStore((state) => state.load);
  const [lead, setLead] = useState<LeadDetails | null>(null);
  const [photoUri, setPhotoUri] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [photoBusy, setPhotoBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setError(null);
      const details = await leadsApi.getLead(route.params.leadId);
      setLead(details);
      setPhotoUri(details.hasPhoto ? await leadsApi.getLeadPhotoDataUri(details.id) : null);
      if (isLeadBusy(details.processingStatus)) {
        const terminal = await leadsApi.waitForLeadTerminalStatus(details.id);
        const refreshed = await leadsApi.getLead(details.id);
        setLead(refreshed);
        if (terminal.processingStatus === 'Failed' && terminal.processingError) {
          setError(terminal.processingError);
        }
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to open this lead.'));
    } finally {
      setLoading(false);
    }
  }, [route.params.leadId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function onAddPhoto() {
    try {
      const picked = await choosePhoto('Lead photo');
      if (!picked) {
        return;
      }

      setPhotoBusy(true);
      const updated = await leadsApi.uploadLeadPhoto(route.params.leadId, picked);
      setLead(updated);
      setPhotoUri(picked.uri);
      await loadLeads();
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to add a photo.'));
    } finally {
      setPhotoBusy(false);
    }
  }

  function onDelete() {
    Alert.alert('Delete lead', 'This lead will be removed.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: () => {
          void (async () => {
            try {
              await leadsApi.deleteLead(route.params.leadId);
              remove(route.params.leadId);
              navigation.goBack();
            } catch (caught) {
              setError(getErrorMessage(caught, 'Unable to delete this lead.'));
            }
          })();
        },
      },
    ]);
  }

  if (loading && !lead) {
    return (
      <Screen scroll={false}>
        <ActivityIndicator color={theme.colors.primary} />
      </Screen>
    );
  }

  return (
    <Screen>
      <Pressable onPress={() => navigation.goBack()}>
        <Text style={[theme.typography.body, { color: theme.colors.primary, fontWeight: '600' }]}>
          Back
        </Text>
      </Pressable>
      {route.params.justFinished ? (
        <Text style={[theme.typography.caption, { color: theme.colors.success, marginTop: 12 }]}>
          This lead is ready
        </Text>
      ) : null}
      {photoUri ? (
        <Image
          source={{ uri: photoUri }}
          style={{
            width: 160,
            height: 160,
            borderRadius: 12,
            marginTop: 16,
            backgroundColor: theme.colors.border,
          }}
        />
      ) : null}
      <Text style={[theme.typography.title, { color: theme.colors.text, marginTop: 12 }]}>
        {leadTitle(lead?.title)}
      </Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 8 }]}>
        {lead ? leadStatusCopy(lead.processingStatus) : ''}
      </Text>
      {error ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>{error}</Text>
      ) : null}
      {lead?.processingError ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>
          {lead.processingError}
        </Text>
      ) : null}
      {lead?.shortSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.text, marginTop: 20 }]}>{lead.shortSummary}</Text>
      ) : null}
      {lead?.fullSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 12 }]}>
          {lead.fullSummary}
        </Text>
      ) : null}
      {lead?.transcript ? (
        <View style={{ marginTop: 24 }}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Transcript</Text>
          <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]}>
            {lead.transcript}
          </Text>
        </View>
      ) : null}
      {lead?.actionItems && lead.actionItems.length > 0 ? (
        <View style={{ marginTop: 24 }}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Action items</Text>
          {lead.actionItems.map((item) => (
            <Text key={item.id} style={[theme.typography.body, { color: theme.colors.text, marginTop: 8 }]}>
              • {item.description}
              {item.dueDate ? ` (${new Date(item.dueDate).toLocaleDateString()})` : ''}
            </Text>
          ))}
        </View>
      ) : null}
      <View style={{ marginTop: 32, gap: 12 }}>
        <PrimaryButton
          label={lead?.hasPhoto ? 'Change photo' : 'Add photo'}
          onPress={() => void onAddPhoto()}
          loading={photoBusy}
        />
        <PrimaryButton label="Delete lead" onPress={onDelete} />
      </View>
    </Screen>
  );
}
