import { useState } from 'react';
import * as Notifications from 'expo-notifications';
import { Platform, StyleSheet, Switch, Text, View } from 'react-native';

import { ApiProblem } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import type { UserProfile } from '@/features/insights/insightsTypes';
import {
  LinkButton,
  PrimaryButton,
  ScreenScaffold,
  SecondaryButton,
  SegmentedControl,
  StatusBanner,
  TextField,
} from '@/shared/ui';

type OnboardingForm = {
  currencyCode: string;
  locale: string;
  timeZone: string;
  monthlyBudgetAmount: string;
};

const currencies = ['USD', 'EUR', 'GBP', 'UAH'] as const;

function deviceDefaults(profile: UserProfile): OnboardingForm {
  const device = Intl.DateTimeFormat().resolvedOptions();
  return {
    currencyCode: profile.currencyCode,
    locale: device.locale || profile.locale,
    timeZone: device.timeZone || profile.timeZone,
    monthlyBudgetAmount: profile.monthlyBudgetAmount > 0 ? profile.monthlyBudgetAmount.toString() : '',
  };
}

async function requestNotificationPermission() {
  if (Platform.OS === 'web') return false;
  const current = await Notifications.getPermissionsAsync();
  if (current.granted) return true;
  const requested = await Notifications.requestPermissionsAsync();
  return requested.granted;
}

export default function OnboardingScreen() {
  const { api, profile, saveProfile } = useInsights();
  const [step, setStep] = useState(0);
  const [form, setForm] = useState<OnboardingForm | null>(() => profile ? deviceDefaults(profile) : null);
  const [notificationOptIn, setNotificationOptIn] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!profile || !form) return null;

  const setField = <Field extends keyof OnboardingForm>(field: Field, value: OnboardingForm[Field]) => {
    setForm((current) => current ? { ...current, [field]: value } : current);
    setError(null);
  };

  const validateEssentials = () => {
    const currency = form.currencyCode.trim().toUpperCase();
    if (!/^[A-Z]{3}$/.test(currency) || !form.locale.trim() || !form.timeZone.trim()) {
      setError('Choose a currency and enter a valid locale and time zone.');
      return false;
    }
    setForm((current) => current ? { ...current, currencyCode: currency } : current);
    setError(null);
    return true;
  };

  const continueFromEssentials = () => {
    if (validateEssentials()) setStep(2);
  };

  const continueFromBudget = (skip: boolean) => {
    const value = skip || !form.monthlyBudgetAmount.trim() ? 0 : Number(form.monthlyBudgetAmount);
    if (!Number.isFinite(value) || value < 0) {
      setError('Enter a non-negative monthly budget or skip this step.');
      return;
    }
    setForm((current) => current ? { ...current, monthlyBudgetAmount: value === 0 ? '' : value.toString() } : current);
    setError(null);
    setStep(3);
  };

  const finish = async () => {
    setBusy(true);
    setError(null);
    try {
      let notificationsGranted = false;
      if (notificationOptIn) {
        try {
          notificationsGranted = await requestNotificationPermission();
          if (notificationsGranted) {
            const preferences = await api.getNotificationPreferences();
            await api.updateNotificationPreferences({ ...preferences, pushEnabled: true });
          }
        } catch {
          notificationsGranted = false;
        }
      }

      await saveProfile({
        locale: form.locale.trim(),
        timeZone: form.timeZone.trim(),
        currencyCode: form.currencyCode.trim().toUpperCase(),
        privacyMode: profile.privacyMode,
        aiPersonalizationEnabled: profile.aiPersonalizationEnabled,
        firstDayOfWeek: profile.firstDayOfWeek,
        monthlyBudgetAmount: form.monthlyBudgetAmount.trim() ? Number(form.monthlyBudgetAmount) : 0,
        budgetNotificationsEnabled: notificationsGranted,
        weeklySummaryNotificationsEnabled: notificationsGranted,
        profileOnboardingCompleted: true,
        preferencesOnboardingCompleted: true,
      });
    } catch (reason) {
      setError(reason instanceof ApiProblem ? reason.message : 'Setup could not be completed. Try again.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScreenScaffold centered={false}>
      {step > 0 ? (
        <View style={styles.navigation}>
          <LinkButton label="Back" onPress={() => setStep((current) => Math.max(0, current - 1))} />
          <Text style={[typography.small, styles.supporting]}>Step {step} of 3</Text>
        </View>
      ) : null}
      {error ? <StatusBanner>{error}</StatusBanner> : null}

      {step === 0 ? (
        <View style={styles.content}>
          <Text accessibilityRole="header" style={[typography.display, styles.title]}>Set up your essentials</Text>
          <Text style={[typography.body, styles.supporting]}>
            Confirm a few defaults so your financial summaries use the right currency and calendar.
          </Text>
          <PrimaryButton label="Start setup" onPress={() => setStep(1)} />
        </View>
      ) : null}

      {step === 1 ? (
        <View style={styles.content}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>Your defaults</Text>
          <SegmentedControl
            label="Currency"
            options={currencies}
            value={form.currencyCode}
            onChange={(value) => setField('currencyCode', value)}
          />
          <TextField
            label="Language and locale"
            value={form.locale}
            onChangeText={(value) => setField('locale', value)}
            autoCapitalize="none"
            editable={!busy}
          />
          <TextField
            label="Time zone"
            value={form.timeZone}
            onChangeText={(value) => setField('timeZone', value)}
            autoCapitalize="none"
            editable={!busy}
          />
          <PrimaryButton label="Continue" onPress={continueFromEssentials} />
        </View>
      ) : null}

      {step === 2 ? (
        <View style={styles.content}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>Monthly budget</Text>
          <Text style={[typography.body, styles.supporting]}>Add a target now or leave it for later.</Text>
          <TextField
            label={`Budget in ${form.currencyCode}`}
            value={form.monthlyBudgetAmount}
            onChangeText={(value) => setField('monthlyBudgetAmount', value)}
            keyboardType="decimal-pad"
            editable={!busy}
          />
          <PrimaryButton label="Continue" onPress={() => continueFromBudget(false)} />
          <SecondaryButton label="Skip budget" onPress={() => continueFromBudget(true)} />
        </View>
      ) : null}

      {step === 3 ? (
        <View style={styles.content}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>Stay informed</Text>
          <View style={styles.toggleRow}>
            <View style={styles.toggleCopy}>
              <Text style={[typography.bodyStrong, styles.title]}>Budget alerts and weekly summaries</Text>
              <Text style={[typography.small, styles.supporting]}>
                Your device asks for permission only after you finish setup with this enabled.
              </Text>
            </View>
            <Switch
              accessibilityLabel="Enable budget alerts and weekly summaries"
              disabled={busy}
              value={notificationOptIn}
              onValueChange={setNotificationOptIn}
              trackColor={{ false: theme.colors.border, true: theme.colors.action }}
              thumbColor={theme.colors.surface}
            />
          </View>
          <PrimaryButton label="Finish setup" loading={busy} onPress={() => void finish()} />
        </View>
      ) : null}
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  navigation: { minHeight: 44, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  content: { flex: 1, justifyContent: 'center', gap: theme.spacing.lg },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  toggleRow: { minHeight: 80, flexDirection: 'row', alignItems: 'center', gap: theme.spacing.md },
  toggleCopy: { flex: 1, gap: theme.spacing.xs },
});
