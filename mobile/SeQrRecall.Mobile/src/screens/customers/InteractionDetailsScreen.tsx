import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Image, Pressable, Text, View } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import * as interactionsApi from '@api/interactionsApi';
import { choosePhoto } from '@services/photoPicker';
import { useInteractionsStore } from '@store/interactionsStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import { interactionStatusCopy, interactionTitle, isInteractionBusy } from '@utils/interactionStatus';
import type { CustomerInteraction } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { CustomersStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<CustomersStackParamList, 'InteractionDetails'>;

export function InteractionDetailsScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const loadTimeline = useInteractionsStore((state) => state.load);
  const [interaction, setInteraction] = useState<CustomerInteraction | null>(null);
  const [photoUri, setPhotoUri] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [photoBusy, setPhotoBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setError(null);
      const details = await interactionsApi.getInteraction(route.params.interactionId);
      setInteraction(details);
      if (details.hasPhoto) {
        const uri = await interactionsApi.getInteractionPhotoDataUri(details.id);
        setPhotoUri(uri);
      } else {
        setPhotoUri(null);
      }
      if (isInteractionBusy(details.processingStatus)) {
        const terminal = await interactionsApi.waitForInteractionTerminalStatus(details.id);
        const refreshed = await interactionsApi.getInteraction(details.id);
        setInteraction(refreshed);
        if (refreshed.hasPhoto) {
          const uri = await interactionsApi.getInteractionPhotoDataUri(refreshed.id);
          setPhotoUri(uri);
        }
        if (terminal.processingStatus === 'Failed' && terminal.processingError) {
          setError(terminal.processingError);
        }
        if (refreshed.customerId) {
          await loadTimeline(refreshed.customerId);
        }
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to open this conversation.'));
    } finally {
      setLoading(false);
    }
  }, [loadTimeline, route.params.interactionId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function onAddPhoto() {
    try {
      const picked = await choosePhoto('Conversation photo');
      if (!picked) {
        return;
      }
      setPhotoBusy(true);
      const updated = await interactionsApi.uploadInteractionPhoto(route.params.interactionId, picked);
      setInteraction(updated);
      setPhotoUri(picked.uri);
      if (updated.customerId) {
        await loadTimeline(updated.customerId);
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to add a photo.'));
    } finally {
      setPhotoBusy(false);
    }
  }

  if (loading && !interaction) {
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
          This conversation is ready
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
        {interactionTitle(interaction?.shortSummary)}
      </Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 8 }]}>
        {interaction ? interactionStatusCopy(interaction.processingStatus) : ''}
        {interaction?.customerName ? ` · ${interaction.customerName}` : ''}
      </Text>
      {error ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>{error}</Text>
      ) : null}
      {interaction?.processingError ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>
          {interaction.processingError}
        </Text>
      ) : null}
      {interaction?.fullSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 12 }]}>
          {interaction.fullSummary}
        </Text>
      ) : interaction?.shortSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.text, marginTop: 20 }]}>
          {interaction.shortSummary}
        </Text>
      ) : null}
      {interaction?.transcript ? (
        <View style={{ marginTop: 24 }}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Transcript</Text>
          <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]}>
            {interaction.transcript}
          </Text>
        </View>
      ) : null}
      {interaction?.actionItems && interaction.actionItems.length > 0 ? (
        <View style={{ marginTop: 24 }}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Action items</Text>
          {interaction.actionItems.map((item) => (
            <Text
              key={item.id}
              style={[theme.typography.body, { color: theme.colors.text, marginTop: 8 }]}
            >
              • {item.description}
              {item.dueDate ? ` (${new Date(item.dueDate).toLocaleDateString()})` : ''}
            </Text>
          ))}
        </View>
      ) : null}
      <View style={{ marginTop: 32 }}>
        <PrimaryButton
          label={photoUri ? 'Change photo' : 'Add photo'}
          onPress={() => void onAddPhoto()}
          loading={photoBusy}
        />
      </View>
    </Screen>
  );
}
