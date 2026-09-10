import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import { TextField } from '@components/TextField';
import { APP_NAME, APP_TAGLINE } from '@constants/index';
import { useAuthStore } from '@store/authStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import {
  isValidIdentifier,
  normalizeIdentifier,
  type AuthChannel,
} from '@utils/identifier';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AuthStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<AuthStackParamList, 'Identifier'>;

export function IdentifierScreen({ navigation }: Props) {
  const theme = useTheme();
  const sendOtp = useAuthStore((state) => state.sendOtp);
  const [channel, setChannel] = useState<AuthChannel>('mobile');
  const [value, setValue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onContinue() {
    const normalized = normalizeIdentifier(channel, value);
    if (!isValidIdentifier(channel, normalized)) {
      setError(channel === 'email' ? 'Enter a valid email address.' : 'Enter a valid mobile number.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const identifier = { channel, value: normalized };
      await sendOtp(identifier);
      navigation.navigate('Otp', { identifier });
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to send a verification code.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <Screen>
      <Text style={[theme.typography.title, { color: theme.colors.text }]}>{APP_NAME}</Text>
      <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8 }]}>
        {APP_TAGLINE}
      </Text>
      <Text style={[theme.typography.heading, { color: theme.colors.text, marginTop: 32, marginBottom: 16 }]}>
        Sign in
      </Text>
      <View style={styles.toggle}>
        <ChannelChip
          label="Mobile"
          selected={channel === 'mobile'}
          onPress={() => {
            setChannel('mobile');
            setError(null);
          }}
        />
        <ChannelChip
          label="Email"
          selected={channel === 'email'}
          onPress={() => {
            setChannel('email');
            setError(null);
          }}
        />
      </View>
      <TextField
        label={channel === 'email' ? 'Email' : 'Mobile number'}
        value={value}
        onChangeText={setValue}
        placeholder={channel === 'email' ? 'you@example.com' : '+91 98765 43210'}
        keyboardType={channel === 'email' ? 'email-address' : 'phone-pad'}
        autoComplete={channel === 'email' ? 'email' : 'tel'}
      />
      {error ? (
        <Text style={[theme.typography.caption, { color: theme.colors.danger, marginBottom: 12 }]}>
          {error}
        </Text>
      ) : null}
      <PrimaryButton label="Send code" onPress={onContinue} loading={loading} />
      <Text style={[theme.typography.caption, { color: theme.colors.mutedText, marginTop: 16 }]}>
        We will send a one-time code. There is no password.
      </Text>
    </Screen>
  );
}

function ChannelChip({
  label,
  selected,
  onPress,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  const theme = useTheme();
  return (
    <Pressable
      onPress={onPress}
      style={[
        styles.chip,
        {
          backgroundColor: selected ? theme.colors.primary : theme.colors.surface,
          borderColor: selected ? theme.colors.primary : theme.colors.border,
        },
      ]}
    >
      <Text
        style={[
          theme.typography.body,
          { color: selected ? theme.colors.primaryText : theme.colors.text, fontWeight: '600' },
        ]}
      >
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  toggle: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 16,
  },
  chip: {
    flex: 1,
    borderWidth: 1,
    borderRadius: 12,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
