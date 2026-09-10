import * as Keychain from 'react-native-keychain';
import type { AuthTokens } from '@models/index';

const SERVICE = 'com.seqr.recall.auth';
const USERNAME = 'session';

export async function saveTokens(tokens: AuthTokens): Promise<void> {
  await Keychain.setGenericPassword(USERNAME, JSON.stringify(tokens), {
    service: SERVICE,
    accessible: Keychain.ACCESSIBLE.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  });
}

export async function loadTokens(): Promise<AuthTokens | null> {
  const stored = await Keychain.getGenericPassword({ service: SERVICE });
  if (!stored) {
    return null;
  }

  try {
    const parsed = JSON.parse(stored.password) as AuthTokens;
    if (!parsed.accessToken || !parsed.refreshToken) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export async function clearTokens(): Promise<void> {
  await Keychain.resetGenericPassword({ service: SERVICE });
}
