import { Linking, PermissionsAndroid, Platform } from 'react-native';

export type MicrophonePermissionResult = 'granted' | 'denied' | 'blocked';

export async function requestMicrophonePermission(): Promise<MicrophonePermissionResult> {
  if (Platform.OS !== 'android') {
    return 'granted';
  }

  const result = await PermissionsAndroid.request(PermissionsAndroid.PERMISSIONS.RECORD_AUDIO, {
    title: 'Microphone',
    message: 'SeQr Recall needs the microphone so you can speak a note.',
    buttonPositive: 'Allow',
    buttonNegative: 'Not now',
  });

  if (result === PermissionsAndroid.RESULTS.GRANTED) {
    return 'granted';
  }

  if (result === PermissionsAndroid.RESULTS.NEVER_ASK_AGAIN) {
    return 'blocked';
  }

  return 'denied';
}

export function microphonePermissionMessage(result: MicrophonePermissionResult): string {
  if (result === 'blocked') {
    return 'Microphone access is turned off. Enable it in Settings to record a note.';
  }

  return 'Microphone access is needed to record a note.';
}

export async function openAppSettings(): Promise<void> {
  await Linking.openSettings();
}
