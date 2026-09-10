export type AuthChannel = 'mobile' | 'email';

export interface AuthIdentifier {
  channel: AuthChannel;
  value: string;
}

export function normalizeIdentifier(channel: AuthChannel, raw: string): string {
  const trimmed = raw.trim();
  if (channel === 'email') {
    return trimmed.toLowerCase();
  }

  return trimmed.replace(/\s+/g, '');
}

export function isValidIdentifier(channel: AuthChannel, value: string): boolean {
  if (channel === 'email') {
    return value.includes('@') && value.length <= 256;
  }

  return value.length >= 8 && value.length <= 20;
}
