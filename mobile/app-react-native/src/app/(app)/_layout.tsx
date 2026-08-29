import { Stack, router, usePathname } from 'expo-router';
import { useEffect } from 'react';
import { StyleSheet, Text } from 'react-native';

import { theme, typography } from '@/app/theme';
import { CaptureProvider } from '@/features/capture/CaptureProvider';
import { InsightsProvider, useInsights } from '@/features/insights/InsightsProvider';
import { useLocalization } from '@/localization/localization';
import { LoadingSkeleton, PrimaryButton, ScreenScaffold, StatusBanner } from '@/shared/ui';

function SignedInNavigator() {
  const pathname = usePathname();
  const { profile, refresh, state } = useInsights();
  const { t } = useLocalization(profile?.locale);
  const onboardingComplete = Boolean(
    profile?.profileOnboardingCompleted && profile.preferencesOnboardingCompleted,
  );
  const needsRedirect = Boolean(
    profile && ((!onboardingComplete && pathname !== '/onboarding') || (onboardingComplete && pathname === '/onboarding')),
  );

  useEffect(() => {
    if (!profile) return;
    if (!onboardingComplete && pathname !== '/onboarding') router.replace('/onboarding');
    if (onboardingComplete && pathname === '/onboarding') router.replace('/home');
  }, [onboardingComplete, pathname, profile]);

  if ((!profile && state === 'loading') || needsRedirect) {
    return (
      <ScreenScaffold>
        <LoadingSkeleton label={t('shell.loadingProfile')} rows={4} />
      </ScreenScaffold>
    );
  }

  if (!profile) {
    return (
      <ScreenScaffold>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>{t('shell.profileUnavailable')}</Text>
        <StatusBanner>{t('shell.profileUnavailableBody')}</StatusBanner>
        <PrimaryButton label={t('common.retry')} onPress={() => void refresh()} />
      </ScreenScaffold>
    );
  }

  return <Stack screenOptions={{ headerShown: false }} />;
}

export default function SignedInLayout() {
  return (
    <InsightsProvider>
      <CaptureProvider>
        <SignedInNavigator />
      </CaptureProvider>
    </InsightsProvider>
  );
}

const styles = StyleSheet.create({
  title: { color: theme.colors.textPrimary },
});
