import { Alert } from 'react-native';
import type { ProcessRecordingResult, SyncPendingResult } from '@services/recordingSync';
import { OFFLINE_SAVED_MESSAGE } from '@utils/errors';

export function acknowledgeSync(result: SyncPendingResult): void {
  if (result.remaining === 0 && result.uploaded > 0) {
    Alert.alert(
      'Synced',
      result.uploaded === 1
        ? '1 recording was sent to the server.'
        : `${result.uploaded} recordings were sent to the server.`,
    );
    return;
  }

  if (result.remaining === 0) {
    Alert.alert('Synced', 'Nothing was waiting to send.');
    return;
  }

  if (result.uploaded > 0) {
    Alert.alert(
      'Partly synced',
      `${result.uploaded} sent. ${result.remaining} still saved on this phone.${
        result.lastError ? `\n\n${result.lastError}` : ''
      }`,
    );
    return;
  }

  Alert.alert(
    result.offline ? 'Saved on this phone' : 'Not synced yet',
    result.lastError ?? OFFLINE_SAVED_MESSAGE,
  );
}

export function acknowledgeRetry(result: ProcessRecordingResult): void {
  if (result.status === 'uploaded') {
    Alert.alert('Synced', 'This recording was sent to the server.');
    return;
  }

  Alert.alert(result.offline ? 'Saved on this phone' : 'Not sent yet', result.message);
}
