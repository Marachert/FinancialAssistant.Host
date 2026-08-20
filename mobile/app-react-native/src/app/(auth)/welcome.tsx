import { router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { LinkButton, PrimaryButton, ScreenScaffold } from '@/shared/ui';

export default function WelcomeScreen() {
  return (
    <ScreenScaffold>
      <View style={styles.content}>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Financial Assistant</Text>
        <Text style={[typography.body, styles.body]}>Capture money activity quickly and understand your financial progress.</Text>
        <Text style={[typography.small, styles.body]}>Suggested fields remain drafts until you confirm them. Backend calculations remain authoritative.</Text>
      </View>
      <PrimaryButton label="Create account" onPress={() => router.push('/register')} />
      <LinkButton label="Sign in" onPress={() => router.push('/sign-in')} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  content: { gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  body: { color: theme.colors.textSecondary },
});
