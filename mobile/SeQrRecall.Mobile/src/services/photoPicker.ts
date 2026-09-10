import { Alert } from 'react-native';
import { launchCamera, launchImageLibrary } from 'react-native-image-picker';
import { MAX_PHOTO_BYTES } from '@constants/index';
import {
  cameraPermissionMessage,
  openAppSettings,
  requestCameraPermission,
} from '@services/cameraPermission';
import type { PhotoUpload } from '@api/customersApi';

const ALLOWED_TYPES = new Set(['image/jpeg', 'image/jpg', 'image/png', 'image/webp']);

export async function choosePhoto(title = 'Photo'): Promise<PhotoUpload | null> {
  const source = await askPhotoSource(title);
  if (!source) {
    return null;
  }

  if (source === 'camera') {
    const permission = await requestCameraPermission();
    if (permission !== 'granted') {
      Alert.alert(
        'Camera',
        cameraPermissionMessage(permission),
        permission === 'blocked'
          ? [
              { text: 'Not now', style: 'cancel' },
              { text: 'Open Settings', onPress: () => void openAppSettings() },
            ]
          : [{ text: 'OK' }],
      );
      return null;
    }

    const result = await launchCamera({
      mediaType: 'photo',
      cameraType: 'back',
      quality: 0.8,
      maxWidth: 1600,
      maxHeight: 1600,
    });
    return toUpload(result.assets?.[0]);
  }

  const result = await launchImageLibrary({
    mediaType: 'photo',
    quality: 0.8,
    maxWidth: 1600,
    maxHeight: 1600,
    selectionLimit: 1,
  });
  return toUpload(result.assets?.[0]);
}

function askPhotoSource(title: string): Promise<'camera' | 'library' | null> {
  return new Promise((resolve) => {
    Alert.alert(title, 'Choose a photo.', [
      { text: 'Cancel', style: 'cancel', onPress: () => resolve(null) },
      { text: 'Photo library', onPress: () => resolve('library') },
      { text: 'Take photo', onPress: () => resolve('camera') },
    ]);
  });
}

function toUpload(asset: { uri?: string; fileName?: string; type?: string; fileSize?: number } | undefined): PhotoUpload | null {
  if (!asset?.uri) {
    return null;
  }

  const type = (asset.type ?? 'image/jpeg').toLowerCase();
  if (!ALLOWED_TYPES.has(type) && !asset.fileName?.match(/\.(jpe?g|png|webp)$/i)) {
    Alert.alert('Photo', 'Use a JPEG, PNG, or WebP photo.');
    return null;
  }

  if (asset.fileSize && asset.fileSize > MAX_PHOTO_BYTES) {
    Alert.alert('Photo', 'That photo is too large. Choose one under 5 MB.');
    return null;
  }

  const name = asset.fileName?.trim() || `photo${extensionFor(type)}`;
  return {
    uri: asset.uri,
    name,
    type: type === 'image/jpg' ? 'image/jpeg' : type,
  };
}

function extensionFor(type: string): string {
  if (type.includes('png')) {
    return '.png';
  }
  if (type.includes('webp')) {
    return '.webp';
  }
  return '.jpg';
}
