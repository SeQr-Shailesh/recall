import { useEffect, useRef, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import { MAX_RECORDING_SECONDS } from '@constants/index';
import {
  cancelRecording,
  startRecording,
  stopRecording,
} from '@services/audioRecorder';
import { submitInteractionRecording } from '@services/recordingSync';
import {
  microphonePermissionMessage,
  openAppSettings,
  requestMicrophonePermission,
} from '@services/microphonePermission';
import { useInteractionsStore } from '@store/interactionsStore';
import { usePendingRecordingsStore } from '@store/pendingRecordingsStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { CustomersStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<CustomersStackParamList, 'RecordInteraction'>;
type Phase = 'idle' | 'recording' | 'working';

export function RecordInteractionScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const loadInteractions = useInteractionsStore((state) => state.load);
  const keepRecording = usePendingRecordingsStore((state) => state.keep);
  const [phase, setPhase] = useState<Phase>('idle');
  const [statusLine, setStatusLine] = useState('Tap to speak');
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState(false);
  const elapsedRef = useRef(0);
  const busyRef = useRef(false);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
      void cancelRecording();
    };
  }, []);

  function clearTimer() {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
  }

  async function onPressMain() {
    setError(null);
    setBlocked(false);
    if (phase === 'idle') {
      const permission = await requestMicrophonePermission();
      if (permission !== 'granted') {
        setBlocked(permission === 'blocked');
        setError(microphonePermissionMessage(permission));
        return;
      }

      try {
        await startRecording();
        elapsedRef.current = 0;
        setPhase('recording');
        setStatusLine('Listening...');
        timerRef.current = setInterval(() => {
          elapsedRef.current += 1;
          if (elapsedRef.current >= MAX_RECORDING_SECONDS) {
            void finishAndUpload();
          }
        }, 1000);
      } catch (caught) {
        setError(getErrorMessage(caught, 'Unable to start recording.'));
      }
      return;
    }

    if (phase === 'recording') {
      await finishAndUpload();
    }
  }

  async function finishAndUpload() {
    if (busyRef.current) {
      return;
    }

    busyRef.current = true;
    clearTimer();
    setPhase('working');
    setStatusLine('Saving this conversation...');
    try {
      const recording = await stopRecording();
      const local = await keepRecording(recording, {
        kind: 'interaction',
        customerId: route.params.customerId,
      });
      setStatusLine('Understanding this conversation...');
      const result = await submitInteractionRecording(local, setStatusLine);
      void loadInteractions(route.params.customerId).catch(() => undefined);
      if (result.status === 'saved-locally') {
        Alert.alert('Conversation saved on this phone', result.message, [
          { text: 'OK', onPress: () => navigation.goBack() },
        ]);
        return;
      }

      if (result.status === 'uploaded-waiting') {
        navigation.replace('InteractionDetails', { interactionId: result.remoteId });
        return;
      }

      if (result.processingStatus === 'Failed') {
        busyRef.current = false;
        setPhase('idle');
        setStatusLine('Tap to speak');
        setError(result.processingError ?? 'Couldn’t save this conversation. Try speaking again.');
        return;
      }

      navigation.replace('InteractionDetails', { interactionId: result.remoteId, justFinished: true });
    } catch (caught) {
      busyRef.current = false;
      setPhase('idle');
      setStatusLine('Tap to speak');
      setError(getErrorMessage(caught, 'Unable to save this conversation.'));
    }
  }

  const listening = phase === 'recording';
  const working = phase === 'working';

  return (
    <Screen>
      <Pressable onPress={() => navigation.goBack()} disabled={working}>
        <Text style={[theme.typography.body, { color: theme.colors.primary, fontWeight: '600' }]}>
          Back
        </Text>
      </Pressable>
      <View style={styles.center}>
        <Text style={[theme.typography.title, { color: theme.colors.text, textAlign: 'center' }]}>
          {statusLine}
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={statusLine}
          disabled={working}
          onPress={() => void onPressMain()}
          style={[
            styles.orb,
            {
              backgroundColor: listening ? theme.colors.recording : theme.colors.primary,
              opacity: working ? 0.6 : 1,
            },
          ]}
        />
        <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 16 }]}>
          {listening
            ? 'Tap again when you are done.'
            : working
              ? ''
              : 'Speak naturally. Mixed languages are fine.'}
        </Text>
      </View>
      {error ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, textAlign: 'center' }]}>{error}</Text>
      ) : null}
      {blocked ? (
        <View style={{ marginTop: 16 }}>
          <PrimaryButton label="Open Settings" onPress={() => void openAppSettings()} />
        </View>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  center: {
    flexGrow: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 48,
  },
  orb: {
    width: 96,
    height: 96,
    borderRadius: 48,
    marginTop: 32,
  },
});
