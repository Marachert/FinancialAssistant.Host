import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { router } from 'expo-router';
import * as Notifications from 'expo-notifications';
import { AppState, Linking, Platform, StyleSheet, Switch, Text, View } from 'react-native';

import { friendlyApiError } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useAuth } from '@/features/auth/AuthProvider';
import { useInsights } from '@/features/insights/InsightsProvider';
import type { NotificationPreferences, UserProfile } from '@/features/insights/insightsTypes';
import { useLocalization } from '@/localization/localization';
import {
  LinkButton,
  LoadingSkeleton,
  PrimaryButton,
  ScreenScaffold,
  SecondaryButton,
  SegmentedControl,
  StatusBanner,
  TextField,
} from '@/shared/ui';

const privacyPolicyUrl = 'https://github.com/Marachert/FinancialAssistant.Host/blob/main/docs/legal/privacy-policy.md';
const supportUrl = 'https://github.com/Marachert/FinancialAssistant.Host/issues';

type SettingsForm = {
  locale: string;
  timeZone: string;
  currencyCode: string;
  privacyMode: 'standard' | 'strict';
  monthlyBudgetAmount: string;
  aiPersonalizationEnabled: boolean;
  budgetNotificationsEnabled: boolean;
  weeklySummaryNotificationsEnabled: boolean;
};

type DevicePermission = {
  status: 'granted' | 'denied' | 'undetermined' | 'unavailable';
  canAskAgain: boolean;
};

function formFromProfile(profile: UserProfile): SettingsForm {
  return {
    locale: profile.locale,
    timeZone: profile.timeZone,
    currencyCode: profile.currencyCode,
    privacyMode: profile.privacyMode,
    monthlyBudgetAmount: profile.monthlyBudgetAmount.toString(),
    aiPersonalizationEnabled: profile.aiPersonalizationEnabled,
    budgetNotificationsEnabled: profile.budgetNotificationsEnabled,
    weeklySummaryNotificationsEnabled: profile.weeklySummaryNotificationsEnabled,
  };
}

function ToggleRow({
  label,
  supporting,
  value,
  disabled,
  onChange,
}: {
  label: string;
  supporting: ReactNode;
  value: boolean;
  disabled?: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <View style={styles.toggleRow}>
      <View style={styles.toggleCopy}>
        <Text style={[typography.bodyStrong, styles.title]}>{label}</Text>
        <Text style={[typography.small, styles.supporting]}>{supporting}</Text>
      </View>
      <Switch
        accessibilityLabel={label}
        disabled={disabled}
        value={value}
        onValueChange={onChange}
        trackColor={{ false: theme.colors.border, true: theme.colors.action }}
        thumbColor={theme.colors.surface}
      />
    </View>
  );
}

export default function SettingsScreen() {
  const { signOut } = useAuth();
  const { api, state, profile, refresh } = useInsights();
  const { t } = useLocalization(profile?.locale);
  const [formOverride, setFormOverride] = useState<SettingsForm | null>(null);
  const form = formOverride ?? (profile ? formFromProfile(profile) : null);
  const [notifications, setNotifications] = useState<NotificationPreferences | null>(null);
  const [notificationError, setNotificationError] = useState<string | null>(null);
  const [notificationLoading, setNotificationLoading] = useState(false);
  const [devicePermission, setDevicePermission] = useState<DevicePermission>({
    status: 'undetermined',
    canAskAgain: true,
  });
  const [permissionBusy, setPermissionBusy] = useState(false);
  const [permissionError, setPermissionError] = useState<string | null>(null);
  const [notificationRationaleVisible, setNotificationRationaleVisible] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const loadNotifications = useCallback(async () => {
    setNotificationLoading(true);
    setNotificationError(null);
    try {
      setNotifications(await api.getNotificationPreferences());
    } catch (reason) {
      setNotificationError(friendlyApiError(reason, 'Notification preferences could not be loaded. Try again.'));
    } finally {
      setNotificationLoading(false);
    }
  }, [api]);

  const loadDevicePermission = useCallback(async () => {
    if (Platform.OS === 'web') {
      setDevicePermission({ status: 'unavailable', canAskAgain: false });
      return;
    }

    try {
      const permission = await Notifications.getPermissionsAsync();
      setDevicePermission({
        status: permission.granted
          ? 'granted'
          : permission.status === 'denied'
            ? 'denied'
            : 'undetermined',
        canAskAgain: permission.canAskAgain,
      });
    } catch {
      setPermissionError('Device notification permission could not be checked.');
    }
  }, []);

  useEffect(() => {
    const initialLoad = setTimeout(() => {
      void loadNotifications();
      void loadDevicePermission();
    }, 0);
    const appStateSubscription = AppState.addEventListener('change', (nextState) => {
      if (nextState === 'active') void loadDevicePermission();
    });
    return () => {
      clearTimeout(initialLoad);
      appStateSubscription.remove();
    };
  }, [loadDevicePermission, loadNotifications]);

  const requestDevicePermission = async () => {
    setPermissionBusy(true);
    setPermissionError(null);
    try {
      const permission = await Notifications.requestPermissionsAsync();
      setNotificationRationaleVisible(false);
      setDevicePermission({
        status: permission.granted
          ? 'granted'
          : permission.status === 'denied'
            ? 'denied'
            : 'undetermined',
        canAskAgain: permission.canAskAgain,
      });
    } catch {
      setPermissionError('Device notification permission could not be requested.');
    } finally {
      setPermissionBusy(false);
    }
  };

  const openNotificationSettings = async () => {
    setPermissionError(null);
    try {
      await Linking.openSettings();
    } catch {
      setPermissionError(t('permissions.settingsFailed'));
    }
  };

  const setField = <Field extends keyof SettingsForm>(field: Field, value: SettingsForm[Field]) => {
    setFormOverride((current) => ({ ...current ?? form!, [field]: value }));
    setError(null);
    setSaved(false);
  };

  const save = async () => {
    if (!form || !profile) return;
    const currency = form.currencyCode.trim().toUpperCase();
    const monthlyBudget = Number(form.monthlyBudgetAmount);
    if (!/^[A-Z]{3}$/.test(currency) || !Number.isFinite(monthlyBudget) || monthlyBudget < 0) {
      setError('Use a three-letter currency and a non-negative monthly budget.');
      return;
    }
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      const saves: Promise<unknown>[] = [
        api.updateProfile({
          locale: form.locale.trim(),
          timeZone: form.timeZone.trim(),
          currencyCode: currency,
          privacyMode: form.privacyMode,
          aiPersonalizationEnabled: form.aiPersonalizationEnabled,
          firstDayOfWeek: profile.firstDayOfWeek,
          monthlyBudgetAmount: monthlyBudget,
          budgetNotificationsEnabled: form.budgetNotificationsEnabled,
          weeklySummaryNotificationsEnabled: form.weeklySummaryNotificationsEnabled,
          profileOnboardingCompleted: profile.profileOnboardingCompleted,
          preferencesOnboardingCompleted: true,
        }),
      ];
      if (notifications) saves.push(api.updateNotificationPreferences(notifications));
      await Promise.all(saves);
      setSaved(true);
      await refresh();
      setFormOverride(null);
    } catch (reason) {
      setError(friendlyApiError(reason, 'Settings could not be saved. Try again.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScreenScaffold centered={false}>
      <View style={styles.header}>
        <LinkButton label="Back" onPress={() => router.back()} />
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Settings</Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {notificationError ? <StatusBanner tone="warning">{notificationError}</StatusBanner> : null}
      {permissionError ? <StatusBanner tone="warning">{permissionError}</StatusBanner> : null}
      {notificationError ? (
        <SecondaryButton
          label="Retry notifications"
          disabled={notificationLoading}
          onPress={() => void loadNotifications()}
        />
      ) : null}
      {saved ? <StatusBanner tone="success">Settings saved.</StatusBanner> : null}

      {!form && state === 'loading' ? (
        <LoadingSkeleton label="Loading settings" rows={4} />
      ) : null}

      {form ? (
        <>
          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>Profile preferences</Text>
            <TextField label="Currency" value={form.currencyCode} onChangeText={(value) => setField('currencyCode', value)} autoCapitalize="characters" editable={!busy} />
            <TextField label="Locale" value={form.locale} onChangeText={(value) => setField('locale', value)} autoCapitalize="none" editable={!busy} />
            <TextField label="Time zone" value={form.timeZone} onChangeText={(value) => setField('timeZone', value)} autoCapitalize="none" editable={!busy} />
            <TextField label="Monthly budget" value={form.monthlyBudgetAmount} onChangeText={(value) => setField('monthlyBudgetAmount', value)} keyboardType="decimal-pad" editable={!busy} />
          </View>

          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>Privacy</Text>
            <SegmentedControl
              label="Privacy mode"
              options={['standard', 'strict']}
              value={form.privacyMode}
              onChange={(value) => setField('privacyMode', value as SettingsForm['privacyMode'])}
            />
            <ToggleRow
              label="AI personalization"
              supporting="Allows optional wording personalization; financial calculations remain deterministic."
              value={form.aiPersonalizationEnabled}
              disabled={busy}
              onChange={(value) => setField('aiPersonalizationEnabled', value)}
            />
            <View style={styles.sectionHeader}>
              <LinkButton label="Privacy policy" onPress={() => void Linking.openURL(privacyPolicyUrl)} />
              <LinkButton label="Support" onPress={() => void Linking.openURL(supportUrl)} />
            </View>
          </View>

          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[typography.heading, styles.title]}>Notifications</Text>
              <LinkButton label="Inbox" onPress={() => router.push('/notifications')} />
            </View>
            <ToggleRow
              label="Budget alerts"
              supporting="Receive alerts as configured spending limits approach."
              value={form.budgetNotificationsEnabled}
              disabled={busy}
              onChange={(value) => setField('budgetNotificationsEnabled', value)}
            />
            <ToggleRow
              label="Weekly summary"
              supporting="Receive a weekly financial summary when delivery is enabled."
              value={form.weeklySummaryNotificationsEnabled}
              disabled={busy}
              onChange={(value) => setField('weeklySummaryNotificationsEnabled', value)}
            />
            {notifications ? (
              <>
                <ToggleRow
                  label="Push delivery"
                  supporting="Allow prepared notifications on this channel."
                  value={notifications.pushEnabled}
                  disabled={busy}
                  onChange={(value) => setNotifications((current) => current ? { ...current, pushEnabled: value } : current)}
                />
                <ToggleRow
                  label="Web delivery"
                  supporting="Allow prepared notifications in the web client."
                  value={notifications.webEnabled}
                  disabled={busy}
                  onChange={(value) => setNotifications((current) => current ? { ...current, webEnabled: value } : current)}
                />
                <View style={styles.devicePermission}>
                  <Text style={[typography.bodyStrong, styles.title]}>{t('permissions.thisDevice')}</Text>
                  <Text style={[typography.small, styles.supporting]}>
                    {devicePermission.status === 'granted'
                      ? t('permissions.notificationAllowed')
                      : devicePermission.status === 'denied'
                        ? t('permissions.notificationDenied')
                        : devicePermission.status === 'unavailable'
                          ? t('permissions.notificationUnavailable')
                          : t('permissions.notificationNotGranted')}
                  </Text>
                  {notifications.pushEnabled && devicePermission.status !== 'granted' ? (
                    <StatusBanner tone="info">
                      {t('permissions.notificationMismatch')}
                    </StatusBanner>
                  ) : null}
                  {devicePermission.status !== 'granted' &&
                  devicePermission.status !== 'unavailable' &&
                  devicePermission.canAskAgain ? (
                    notificationRationaleVisible ? (
                      <View style={styles.permissionFlow}>
                        <Text style={[typography.body, styles.supporting]}>
                          {t('permissions.notificationRationale')}
                        </Text>
                        <PrimaryButton
                          label={t('permissions.continue')}
                          loading={permissionBusy}
                          onPress={() => void requestDevicePermission()}
                        />
                        <SecondaryButton
                          label={t('permissions.notNow')}
                          disabled={permissionBusy}
                          onPress={() => setNotificationRationaleVisible(false)}
                        />
                      </View>
                    ) : (
                      <SecondaryButton
                        label={t('permissions.reviewNotificationAccess')}
                        disabled={permissionBusy}
                        onPress={() => setNotificationRationaleVisible(true)}
                      />
                    )
                  ) : null}
                  {devicePermission.status === 'denied' && !devicePermission.canAskAgain ? (
                    <SecondaryButton
                      label={t('permissions.openSettings')}
                      onPress={() => void openNotificationSettings()}
                    />
                  ) : null}
                </View>
              </>
            ) : null}
          </View>

          <PrimaryButton label="Save settings" loading={busy} onPress={() => void save()} />
        </>
      ) : (
        state === 'error' ? <SecondaryButton label="Retry settings" onPress={() => void refresh()} /> : null
      )}
      <SecondaryButton label="Sign out" disabled={busy} onPress={() => void signOut()} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { minHeight: 48, flexDirection: 'row', alignItems: 'center', gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  section: { gap: theme.spacing.md, paddingBottom: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  sectionHeader: { minHeight: 44, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: theme.spacing.md },
  devicePermission: { gap: theme.spacing.sm, paddingTop: theme.spacing.sm },
  permissionFlow: { gap: theme.spacing.md },
  toggleRow: { minHeight: 64, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: theme.spacing.md },
  toggleCopy: { flex: 1, gap: theme.spacing.xs },
});
