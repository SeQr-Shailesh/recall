import { Platform } from 'react-native';
import { appConfig } from '@config/index';
import { createCorrelationId } from '@utils/correlationId';
import { ApiError, isLikelyOfflineError } from '@utils/errors';
import { loadTokens, saveTokens, clearTokens } from '@services/secureStorage';
import { tokenSession } from '@services/tokenSession';
import type { ApiResponse, AuthTokens } from '@models/index';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface RequestOptions {
  method?: HttpMethod;
  body?: unknown;
  auth?: boolean;
  skipRefresh?: boolean;
}

async function parseEnvelope<T>(response: Response): Promise<ApiResponse<T>> {
  const text = await response.text();
  if (!text) {
    return { success: false, data: null, message: 'Unable to process request.', errors: [] };
  }

  try {
    return JSON.parse(text) as ApiResponse<T>;
  } catch {
    throw new ApiError('Unable to process request.', response.status);
  }
}

async function refreshAccessToken(): Promise<boolean> {
  const stored = await loadTokens();
  if (!stored?.refreshToken) {
    tokenSession.clear();
    return false;
  }

  try {
    const tokens = await request<AuthTokens>('/auth/refresh-token', {
      method: 'POST',
      body: {
        refreshToken: stored.refreshToken,
        deviceInfo: getDeviceInfo(),
      },
      auth: false,
      skipRefresh: true,
    });
    tokenSession.setAccessToken(tokens.accessToken);
    await saveTokens(tokens);
    return true;
  } catch (error) {
    if (isLikelyOfflineError(error)) {
      return false;
    }

    tokenSession.clear();
    await clearTokens();
    return false;
  }
}

function isFormData(body: unknown): body is FormData {
  return typeof FormData !== 'undefined' && body instanceof FormData;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? 'GET';
  const useAuth = options.auth !== false;
  const form = isFormData(options.body);
  const headers: Record<string, string> = {
    Accept: 'application/json',
    'X-Correlation-ID': createCorrelationId(),
  };

  if (options.body !== undefined && !form) {
    headers['Content-Type'] = 'application/json';
  }

  if (useAuth) {
    const token = tokenSession.getAccessToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
  }

  let response: Response;
  try {
    response = await fetch(`${appConfig.apiBaseUrl}${appConfig.apiVersionPrefix}${path}`, {
      method,
      headers,
      body:
        options.body === undefined
          ? undefined
          : form
            ? (options.body as FormData)
            : JSON.stringify(options.body),
    });
  } catch (error) {
    throw new ApiError(
      error instanceof Error ? error.message : 'Network request failed',
      0,
    );
  }

  if (response.status === 401 && useAuth && !options.skipRefresh) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      return request<T>(path, { ...options, skipRefresh: true });
    }
  }

  const envelope = await parseEnvelope<T>(response);
  if (!response.ok || !envelope.success) {
    throw new ApiError(
      envelope.message ?? 'Unable to process request.',
      response.status,
      envelope.errors ?? [],
    );
  }

  return envelope.data as T;
}

async function requestBinary(
  path: string,
  skipRefresh = false,
): Promise<{ contentType: string; base64: string }> {
  const headers: Record<string, string> = {
    Accept: 'image/*,application/json',
    'X-Correlation-ID': createCorrelationId(),
  };
  const token = tokenSession.getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(`${appConfig.apiBaseUrl}${appConfig.apiVersionPrefix}${path}`, {
      method: 'GET',
      headers,
    });
  } catch (error) {
    throw new ApiError(
      error instanceof Error ? error.message : 'Network request failed',
      0,
    );
  }

  if (response.status === 401 && !skipRefresh) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      return requestBinary(path, true);
    }
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (!response.ok || contentType.toLowerCase().includes('application/json')) {
    const envelope = await parseEnvelope<unknown>(response);
    throw new ApiError(
      envelope.message ?? 'Unable to process request.',
      response.status,
      envelope.errors ?? [],
    );
  }

  const buffer = await response.arrayBuffer();
  return {
    contentType: contentType.split(';')[0]?.trim() || 'application/octet-stream',
    base64: arrayBufferToBase64(buffer),
  };
}

function arrayBufferToBase64(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  const chunkSize = 0x8000;
  let binary = '';
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    const chunk = bytes.subarray(offset, offset + chunkSize);
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
}

export function getDeviceInfo(): string {
  return `SeQrRecall.${Platform.OS}`;
}

export const api = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown, auth = true) =>
    request<T>(path, { method: 'POST', body, auth }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  upload: <T>(path: string, body: FormData) => request<T>(path, { method: 'POST', body }),
  getBinary: (path: string) => requestBinary(path),
};
