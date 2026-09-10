import { Linking, PermissionsAndroid, Platform } from 'react-native';

export type CameraPermissionResult = 'granted' | 'denied' | 'blocked';

export async function requestCameraPermission(): Promise<CameraPermissionResult> {
  if (Platform.OS !== 'android') {
    return 'granted';
  }

  const result = await PermissionsAndroid.request(PermissionsAndroid.PERMISSIONS.CAMERA, {
    title: 'Camera',
    message: 'SeQr Recall needs the camera so you can add a customer photo.',
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

export function cameraPermissionMessage(result: CameraPermissionResult): string {
  if (result === 'blocked') {
    return 'Camera access is turned off. Enable it in Settings to take a photo.';
  }

  return 'Camera access is needed to take a customer photo.';
}

export async function openAppSettings(): Promise<void> {
  await Linking.openSettings();
}
