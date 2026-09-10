import { appConfig } from '@config/index';

const PROBE_TIMEOUT_MS = 4000;

/** True when the API host answers. Used to resume the local recording queue without a native NetInfo module. */
export async function canReachApi(): Promise<boolean> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS);
  try {
    const response = await fetch(`${appConfig.apiBaseUrl}/health`, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });
    return response.ok;
  } catch {
    return false;
  } finally {
    clearTimeout(timer);
  }
}
