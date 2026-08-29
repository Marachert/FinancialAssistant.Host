import { router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useLocalization } from '@/localization/localization';
import { LinkButton, PrimaryButton, ScreenScaffold } from '@/shared/ui';

export default function WelcomeScreen() {
  const { t } = useLocalization();
  return (
    <ScreenScaffold>
      <View style={styles.content}>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>{t('common.productName')}</Text>
        <Text style={[typography.body, styles.body]}>{t('auth.welcomeBody')}</Text>
        <Text style={[typography.small, styles.body]}>{t('auth.welcomeTrust')}</Text>
      </View>
      <PrimaryButton label={t('auth.createAccount')} onPress={() => router.push('/register')} />
      <LinkButton label={t('auth.signIn')} onPress={() => router.push('/sign-in')} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  content: { gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  body: { color: theme.colors.textSecondary },
});
