import { api, getDeviceInfo } from '@api/client';
import type { AuthIdentifier } from '@utils/identifier';
import type { User, VerifyOtpResult } from '@models/index';

export interface UpdateProfileInput {
  fullName: string;
  mobile?: string | null;
  email?: string | null;
}

export function sendOtp(identifier: AuthIdentifier): Promise<{ sent: boolean }> {
  const body =
    identifier.channel === 'email'
      ? { email: identifier.value, mobile: null }
      : { mobile: identifier.value, email: null };
  return api.post<{ sent: boolean }>('/auth/send-otp', body, false);
}

export function verifyOtp(identifier: AuthIdentifier, otp: string): Promise<VerifyOtpResult> {
  const body =
    identifier.channel === 'email'
      ? { email: identifier.value, mobile: null, otp, deviceInfo: getDeviceInfo() }
      : { mobile: identifier.value, email: null, otp, deviceInfo: getDeviceInfo() };
  return api.post<VerifyOtpResult>('/auth/verify-otp', body, false);
}

export function logout(refreshToken: string): Promise<unknown> {
  return api.post('/auth/logout', { refreshToken });
}

export function getCurrentUser(): Promise<User> {
  return api.get<User>('/users/me');
}

export function updateProfile(input: UpdateProfileInput): Promise<User> {
  return api.put<User>('/users/me', input);
}
