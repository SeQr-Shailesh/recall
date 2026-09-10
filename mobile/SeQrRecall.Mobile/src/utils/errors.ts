export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly errors: readonly string[] = [],
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export const OFFLINE_MESSAGE =
  'If there is no network connection or the network is unstable, the app will automatically sync the data once a network connection is restored.';

export const OFFLINE_SAVED_MESSAGE = `Saved on this phone. ${OFFLINE_MESSAGE}`;

export function getErrorMessage(error: unknown, fallback = 'Unable to process request.'): string {
  if (isLikelyOfflineError(error)) {
    return OFFLINE_MESSAGE;
  }

  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}

export function isLikelyOfflineError(error: unknown): boolean {
  if (error instanceof ApiError && error.status === 0) {
    return true;
  }

  const message = error instanceof Error ? error.message : String(error ?? '');
  const normalized = message.toLowerCase();
  return (
    normalized.includes('network request failed') ||
    normalized.includes('failed to fetch') ||
    normalized.includes('network error') ||
    normalized.includes('internet') ||
    normalized.includes('offline') ||
    normalized.includes('timed out') ||
    normalized.includes('timeout')
  );
}

/** Server already accepted this audio; a later retry must not treat that as a hard failure. */
export function isAlreadyUploadedError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '');
  return message.toLowerCase().includes('already being processed');
}
