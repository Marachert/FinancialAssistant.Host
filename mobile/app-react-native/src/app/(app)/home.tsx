import { router } from 'expo-router';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import {
  LinkButton,
  PrimaryButton,
  ScreenScaffold,
  SecondaryButton,
  StatusBanner,
} from '@/shared/ui';

function money(value: number, currency: string) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

function ProgressBar({ value, tone = 'action' }: { value: number; tone?: 'action' | 'warning' }) {
  const width = `${Math.min(100, Math.max(0, value))}%` as `${number}%`;
  return (
    <View
      accessibilityRole="progressbar"
      accessibilityValue={{ min: 0, max: 100, now: Math.round(value) }}
      style={styles.progressTrack}
    >
      <View style={[styles.progressValue, { width }, tone === 'warning' && styles.progressWarning]} />
    </View>
  );
}

export default function HomeScreen() {
  const {
    state,
    refreshing,
    error,
    dashboard,
    score,
    recommendations,
    refresh,
  } = useInsights();
  const recommendation = recommendations.find((item) => item.status === 'active') ?? recommendations[0];

  return (
    <ScreenScaffold
      centered={false}
      refreshing={refreshing}
      onRefresh={() => void refresh()}
    >
      <View style={styles.header}>
        <View style={styles.headerCopy}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>Today</Text>
          <Text style={[typography.small, styles.supporting]}>Financial Assistant</Text>
        </View>
        <LinkButton label="Settings" onPress={() => router.push('/settings')} />
      </View>

      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {state === 'loading' && !dashboard ? (
        <View accessibilityLiveRegion="polite" style={styles.loading}>
          <ActivityIndicator color={theme.colors.action} />
          <Text style={[typography.body, styles.supporting]}>Loading your overview...</Text>
        </View>
      ) : null}

      {dashboard ? (
        <>
          {dashboard.freshness.isStale ? (
            <StatusBanner tone="warning">Some totals are still catching up. Last known values are shown.</StatusBanner>
          ) : null}

          <View style={styles.heroMetric}>
            <Text style={[typography.small, styles.supporting]}>Today spent</Text>
            <Text style={[typography.display, styles.title]}>
              {money(dashboard.dailySummary.expenseTotal, dashboard.currency)}
            </Text>
            {dashboard.dailyLimit.isConfigured && dashboard.dailyLimit.limit !== null ? (
              <>
                <ProgressBar
                  value={dashboard.dailyLimit.usedPercent ?? 0}
                  tone={(dashboard.dailyLimit.usedPercent ?? 0) >= 80 ? 'warning' : 'action'}
                />
                <Text style={[typography.small, styles.supporting]}>
                  {`${money(Math.max(0, dashboard.dailyLimit.remaining ?? 0), dashboard.currency)} left from today's limit`}
                </Text>
              </>
            ) : (
              <Text style={[typography.small, styles.supporting]}>No daily limit configured</Text>
            )}
          </View>

          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>This month</Text>
            <View style={styles.metricGrid}>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>Income</Text>
                <Text style={[typography.bodyStrong, styles.positive]}>
                  {money(dashboard.monthlyProgress.incomeTotal, dashboard.currency)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>Expenses</Text>
                <Text style={[typography.bodyStrong, styles.title]}>
                  {money(dashboard.monthlyProgress.expenseTotal, dashboard.currency)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>Balance change</Text>
                <Text style={[typography.bodyStrong, dashboard.monthlyProgress.balanceDelta >= 0 ? styles.positive : styles.critical]}>
                  {money(dashboard.monthlyProgress.balanceDelta, dashboard.currency)}
                </Text>
              </View>
            </View>
            {dashboard.limitsProgress.monthly.isConfigured ? (
              <ProgressBar value={dashboard.limitsProgress.monthly.usedPercent ?? 0} />
            ) : null}
          </View>

        </>
      ) : null}

      {score ? (
        <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[typography.heading, styles.title]}>Financial score</Text>
              <LinkButton label="View factors" onPress={() => router.push('/score')} />
            </View>
            <View style={styles.scoreRow}>
              <Text style={[typography.display, styles.title]}>{score.score}</Text>
              <Text style={[typography.body, styles.supporting]}>out of 100</Text>
            </View>
            <ProgressBar value={score.score} />
          </View>
      ) : null}

      {dashboard || score || recommendations.length ? (
        <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[typography.heading, styles.title]}>Latest recommendation</Text>
              <LinkButton label="View all" onPress={() => router.push('/recommendations')} />
            </View>
            {recommendation ? (
              <View style={styles.recommendation}>
                <Text style={[typography.bodyStrong, styles.title]}>{recommendation.title}</Text>
                <Text style={[typography.body, styles.supporting]}>{recommendation.body}</Text>
              </View>
            ) : (
              <Text style={[typography.body, styles.supporting]}>No recommendations right now.</Text>
            )}
          </View>
      ) : null}

      {state === 'error' && !dashboard ? (
        <SecondaryButton label="Retry overview" onPress={() => void refresh()} />
      ) : null}
      <PrimaryButton label="Add transaction" onPress={() => router.push('/add')} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { minHeight: 52, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: theme.spacing.md },
  headerCopy: { flex: 1 },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  positive: { color: theme.colors.positive },
  critical: { color: theme.colors.critical },
  loading: { minHeight: 160, alignItems: 'center', justifyContent: 'center', gap: theme.spacing.md },
  heroMetric: { gap: theme.spacing.sm, paddingVertical: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  section: { gap: theme.spacing.md, paddingVertical: theme.spacing.sm },
  sectionHeader: { minHeight: 44, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: theme.spacing.sm },
  metricGrid: { flexDirection: 'row', flexWrap: 'wrap', borderTopWidth: 1, borderColor: theme.colors.border },
  metric: { width: '50%', minHeight: 72, gap: theme.spacing.xs, paddingVertical: theme.spacing.md, paddingRight: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  scoreRow: { minHeight: 48, flexDirection: 'row', alignItems: 'baseline', gap: theme.spacing.sm },
  progressTrack: { height: 8, overflow: 'hidden', borderRadius: theme.radius.control, backgroundColor: theme.colors.surfaceSubtle },
  progressValue: { height: 8, backgroundColor: theme.colors.action },
  progressWarning: { backgroundColor: theme.colors.warning },
  recommendation: { gap: theme.spacing.sm, borderLeftWidth: 4, borderColor: theme.colors.info, paddingLeft: theme.spacing.md, paddingVertical: theme.spacing.xs },
});
