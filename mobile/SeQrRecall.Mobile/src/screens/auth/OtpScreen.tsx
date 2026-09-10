import { useState } from 'react';
import { Text } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import { TextField } from '@components/TextField';
import { DEV_OTP } from '@constants/index';
import { useAuthStore } from '@store/authStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AuthStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<AuthStackParamList, 'Otp'>;

export function OtpScreen({ route }: Props) {
  const theme = useTheme();
  const verifyOtp = useAuthStore((state) => state.verifyOtp);
  const [otp, setOtp] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const destination = route.params.identifier.value;

  async function onVerify() {
    const code = otp.trim();
    if (code.length !== 6) {
      setError('Enter the 6-digit code.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      await verifyOtp(code);
    } catch (caught) {
      setError(getErrorMessage(caught, 'The verification code is incorrect.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <Screen>
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Enter the code</Text>
      <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8, marginBottom: 24 }]}>
        We sent a code to {destination}.
      </Text>
      <TextField
        label="Verification code"
        value={otp}
        onChangeText={(value) => setOtp(value.replace(/[^\d]/g, '').slice(0, 6))}
        keyboardType="number-pad"
        autoComplete="sms-otp"
        maxLength={6}
        placeholder="123456"
      />
      {__DEV__ ? (
        <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginBottom: 12 }]}>
          Development code is {DEV_OTP}.
        </Text>
      ) : null}
      {error ? (
        <Text style={[theme.typography.caption, { color: theme.colors.danger, marginBottom: 12 }]}>
          {error}
        </Text>
      ) : null}
      <PrimaryButton label="Verify" onPress={onVerify} loading={loading} disabled={otp.length !== 6} />
    </Screen>
  );
}
