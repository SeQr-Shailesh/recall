import * as Keychain from 'react-native-keychain';
import type { User } from '@models/index';

const SERVICE = 'com.seqr.recall.profile';
const USERNAME = 'user';

export async function saveUserSnapshot(user: User): Promise<void> {
  await Keychain.setGenericPassword(USERNAME, JSON.stringify(user), {
    service: SERVICE,
    accessible: Keychain.ACCESSIBLE.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  });
}

export async function loadUserSnapshot(): Promise<User | null> {
  const stored = await Keychain.getGenericPassword({ service: SERVICE });
  if (!stored) {
    return null;
  }

  try {
    const parsed = JSON.parse(stored.password) as User;
    if (!parsed.id || typeof parsed.isProfileComplete !== 'boolean') {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export async function clearUserSnapshot(): Promise<void> {
  await Keychain.resetGenericPassword({ service: SERVICE });
}
