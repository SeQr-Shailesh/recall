import { useState } from 'react';
import { Text } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import { TextField } from '@components/TextField';
import { useAuthStore } from '@store/authStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';

export function ProfileSetupScreen() {
  const theme = useTheme();
  const user = useAuthStore((state) => state.user);
  const completeProfile = useAuthStore((state) => state.completeProfile);
  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSave() {
    const name = fullName.trim();
    if (!name) {
      setError('Full name is required.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      await completeProfile(name);
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to save your profile.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <Screen>
      <Text style={[theme.typography.heading, { color: theme.colors.text }]}>Your name</Text>
      <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8, marginBottom: 24 }]}>
        This is how notes and customer records will identify you.
      </Text>
      <TextField
        label="Full name"
        value={fullName}
        onChangeText={setFullName}
        autoCapitalize="words"
        autoComplete="name"
        placeholder="Rajesh Kumar"
      />
      {error ? (
        <Text style={[theme.typography.caption, { color: theme.colors.danger, marginBottom: 12 }]}>
          {error}
        </Text>
      ) : null}
      <PrimaryButton label="Continue" onPress={onSave} loading={loading} />
    </Screen>
  );
}
