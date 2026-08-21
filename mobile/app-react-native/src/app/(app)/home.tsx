import { router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useAuth } from '@/features/auth/AuthProvider';
import { PrimaryButton, ScreenScaffold, SecondaryButton } from '@/shared/ui';

export default function HomeScreen() {
  const { signOut } = useAuth();
  return (
    <ScreenScaffold>
      <View style={styles.content}>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Financial Assistant</Text>
        <Text style={[typography.body, styles.body]}>Your secure session is active.</Text>
        <Text style={[typography.small, styles.body]}>Your dashboard will appear here as the next mobile capability is delivered.</Text>
      </View>
      <PrimaryButton label="Add transaction" onPress={() => router.push('/add')} />
      <SecondaryButton label="Sign out" onPress={() => void signOut()} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  content: { gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  body: { color: theme.colors.textSecondary },
});
