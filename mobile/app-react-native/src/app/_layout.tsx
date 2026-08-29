import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { theme } from '@/app/theme';
import { AuthProvider, useAuth } from '@/features/auth/AuthProvider';
import { useLocalization } from '@/localization/localization';

function RootNavigator() {
  const { state } = useAuth();
  const { t } = useLocalization();
  if (state === 'loading') {
    return (
      <View accessibilityLabel={t('shell.restoringSession')} accessibilityRole="progressbar" style={styles.loading}>
        <ActivityIndicator color={theme.colors.action} size="large" />
      </View>
    );
  }

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Protected guard={state === 'anonymous'}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={state === 'authenticated'}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
    </Stack>
  );
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <AuthProvider>
        <StatusBar style="dark" />
        <RootNavigator />
      </AuthProvider>
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.canvas },
});
