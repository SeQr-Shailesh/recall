import { Text } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import { APP_NAME } from '@constants/index';
import { useAuthStore } from '@store/authStore';
import { useTheme } from '@theme/useTheme';

export function HomeScreen() {
  const theme = useTheme();
  const user = useAuthStore((state) => state.user);
  const signOut = useAuthStore((state) => state.signOut);
  const displayName = user?.fullName?.trim() ? user.fullName : 'there';

  return (
    <Screen>
      <Text style={[theme.typography.title, { color: theme.colors.text }]}>{APP_NAME}</Text>
      <Text style={[theme.typography.heading, { color: theme.colors.text, marginTop: 24 }]}>
        Hello, {displayName}
      </Text>
      <Text style={[theme.typography.body, { color: theme.colors.mutedText, marginTop: 8, marginBottom: 32 }]}>
        You are signed in. Voice notes will be added in the next phase.
      </Text>
      <PrimaryButton label="Sign out" onPress={() => void signOut()} />
    </Screen>
  );
}
