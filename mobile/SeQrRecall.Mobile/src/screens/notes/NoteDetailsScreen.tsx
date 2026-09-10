import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, Text, View } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import * as notesApi from '@api/notesApi';
import { useNotesStore } from '@store/notesStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import { isNoteBusy, noteStatusCopy, noteTitle } from '@utils/noteStatus';
import type { NoteDetails } from '@models/index';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AppStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<AppStackParamList, 'NoteDetails'>;

export function NoteDetailsScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const remove = useNotesStore((state) => state.remove);
  const [note, setNote] = useState<NoteDetails | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      setError(null);
      const details = await notesApi.getNote(route.params.noteId);
      setNote(details);
      if (isNoteBusy(details.processingStatus)) {
        const terminal = await notesApi.waitForNoteTerminalStatus(details.id);
        const refreshed = await notesApi.getNote(details.id);
        setNote(refreshed);
        if (terminal.processingStatus === 'Failed' && terminal.processingError) {
          setError(terminal.processingError);
        }
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to open this note.'));
    } finally {
      setLoading(false);
    }
  }, [route.params.noteId]);

  useEffect(() => {
    void load();
  }, [load]);

  function onDelete() {
    Alert.alert('Delete note', 'This note will be removed.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: () => {
          void (async () => {
            try {
              await notesApi.deleteNote(route.params.noteId);
              remove(route.params.noteId);
              navigation.goBack();
            } catch (caught) {
              setError(getErrorMessage(caught, 'Unable to delete this note.'));
            }
          })();
        },
      },
    ]);
  }

  if (loading && !note) {
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
          Your note is ready
        </Text>
      ) : null}
      <Text style={[theme.typography.title, { color: theme.colors.text, marginTop: 12 }]}>
        {noteTitle(note?.title)}
      </Text>
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 8 }]}>
        {note ? noteStatusCopy(note.processingStatus) : ''}
      </Text>
      {error ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>{error}</Text>
      ) : null}
      {note?.processingError ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginTop: 12 }]}>
          {note.processingError}
        </Text>
      ) : null}
      {note?.shortSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.text, marginTop: 20 }]}>{note.shortSummary}</Text>
      ) : null}
      {note?.fullSummary ? (
        <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 12 }]}>
          {note.fullSummary}
        </Text>
      ) : null}
      {note?.transcript ? (
        <View style={{ marginTop: 24 }}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Transcript</Text>
          <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]}>
            {note.transcript}
          </Text>
        </View>
      ) : null}
      {note?.actionItems && note.actionItems.length > 0 ? (
        <View style={{ marginTop: 24 }}>
          <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Action items</Text>
          {note.actionItems.map((item) => (
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
        <PrimaryButton label="Delete note" onPress={onDelete} />
      </View>
    </Screen>
  );
}
