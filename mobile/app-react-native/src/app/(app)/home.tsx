import { useState } from 'react';
import { router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import { formatCurrency, useLocalization } from '@/localization/localization';
import {
  LinkButton,
  LoadingSkeleton,
  PrimaryButton,
  ScreenScaffold,
  SecondaryButton,
  SegmentedControl,
  StatusBanner,
} from '@/shared/ui';

type DashboardPeriod = 'today' | 'week' | 'month';

function ProgressBar({ value, label, tone = 'action' }: { value: number; label: string; tone?: 'action' | 'warning' }) {
  const width = `${Math.min(100, Math.max(0, value))}%` as `${number}%`;
  return (
    <View
      accessibilityLabel={label}
      accessibilityRole="progressbar"
      accessibilityValue={{ min: 0, max: 100, now: Math.round(value) }}
      style={styles.progressTrack}
    >
      <View style={[styles.progressValue, { width }, tone === 'warning' && styles.progressWarning]} />
    </View>
  );
}

export default function HomeScreen() {
  const [period, setPeriod] = useState<DashboardPeriod>('today');
  const {
    state,
    refreshing,
    error,
    dashboard,
    score,
    recommendations,
    profile,
    refresh,
  } = useInsights();
  const { locale, t } = useLocalization(profile?.locale);
  const periodOptions = [
    { value: 'today', label: t('home.today') },
    { value: 'week', label: t('home.week') },
    { value: 'month', label: t('home.month') },
  ] as const;
  const periodName = period === 'today'
    ? t('home.today')
    : period === 'week'
      ? t('home.week')
      : t('home.month');
  const recommendation = recommendations.find((item) => item.status === 'active') ?? recommendations[0];
  const summary = dashboard
    ? period === 'today'
      ? dashboard.dailySummary
      : period === 'week'
        ? dashboard.weeklySummary
        : dashboard.monthlySummary
    : null;
  const limit = dashboard
    ? period === 'today'
      ? dashboard.dailyLimit
      : period === 'week'
        ? dashboard.limitsProgress.weekly
        : dashboard.limitsProgress.monthly
    : null;
  const hasActivity = Boolean(summary && (summary.incomeTotal !== 0 || summary.expenseTotal !== 0));

  return (
    <ScreenScaffold
      centered={false}
      refreshing={refreshing}
      onRefresh={() => void refresh()}
    >
      <View style={styles.header}>
        <View style={styles.headerCopy}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>{t('home.overview')}</Text>
          <Text style={[typography.small, styles.supporting]}>{t('common.productName')}</Text>
        </View>
        <LinkButton label={t('home.settings')} onPress={() => router.push('/settings')} />
      </View>

      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {state === 'loading' && !dashboard ? (
        <LoadingSkeleton label={t('home.loading')} rows={3} />
      ) : null}

      {dashboard && summary && limit ? (
        <>
          {dashboard.freshness.isStale ? (
            <StatusBanner tone="warning">{t('home.stale')}</StatusBanner>
          ) : null}

          <SegmentedControl
            label={t('home.periodLabel')}
            options={periodOptions}
            value={period}
            onChange={(value) => setPeriod(value as DashboardPeriod)}
          />

          <View style={styles.heroMetric}>
            <Text style={[typography.small, styles.supporting]}>{t('home.spent', { period: periodName })}</Text>
            <Text style={[typography.display, styles.title]}>
              {formatCurrency(summary.expenseTotal, dashboard.currency, locale)}
            </Text>
            {limit.isConfigured && limit.limit !== null ? (
              <>
                <ProgressBar
                  label={t('home.spent', { period: periodName })}
                  value={limit.usedPercent ?? 0}
                  tone={(limit.usedPercent ?? 0) >= 80 ? 'warning' : 'action'}
                />
                <Text style={[typography.small, styles.supporting]}>
                  {t('home.limitRemaining', {
                    amount: formatCurrency(Math.max(0, limit.remaining ?? 0), dashboard.currency, locale),
                  })}
                </Text>
              </>
            ) : (
              <Text style={[typography.small, styles.supporting]}>{t('home.noLimit')}</Text>
            )}
          </View>

          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[typography.heading, styles.title]}>{t('home.summary', { period: periodName })}</Text>
              <LinkButton label={t('home.analytics')} onPress={() => router.push('/analytics')} />
            </View>
            <View style={styles.metricGrid}>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>{t('home.income')}</Text>
                <Text style={[typography.bodyStrong, styles.positive]}>
                  {formatCurrency(summary.incomeTotal, dashboard.currency, locale)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>{t('home.expenses')}</Text>
                <Text style={[typography.bodyStrong, styles.title]}>
                  {formatCurrency(summary.expenseTotal, dashboard.currency, locale)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>{t('home.balanceChange')}</Text>
                <Text style={[typography.bodyStrong, summary.balanceDelta >= 0 ? styles.positive : styles.critical]}>
                  {formatCurrency(summary.balanceDelta, dashboard.currency, locale)}
                </Text>
              </View>
            </View>
            {!hasActivity ? (
              <StatusBanner tone="info">{t('home.noActivity')}</StatusBanner>
            ) : null}
          </View>

        </>
      ) : null}

      {score ? (
        <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[typography.heading, styles.title]}>{t('home.financialScore')}</Text>
              <LinkButton label={t('home.viewFactors')} onPress={() => router.push('/score')} />
            </View>
            <View style={styles.scoreRow}>
              <Text style={[typography.display, styles.title]}>{score.score}</Text>
              <Text style={[typography.body, styles.supporting]}>{t('home.outOf100')}</Text>
            </View>
            <ProgressBar label={t('home.financialScore')} value={score.score} />
          </View>
      ) : null}

      {dashboard || score || recommendations.length ? (
        <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[typography.heading, styles.title]}>{t('home.latestRecommendation')}</Text>
              <LinkButton label={t('home.viewAll')} onPress={() => router.push('/recommendations')} />
            </View>
            {recommendation ? (
              <View style={styles.recommendation}>
                <Text style={[typography.bodyStrong, styles.title]}>{recommendation.title}</Text>
                <Text style={[typography.body, styles.supporting]}>{recommendation.body}</Text>
              </View>
            ) : (
              <Text style={[typography.body, styles.supporting]}>{t('home.noRecommendations')}</Text>
            )}
          </View>
      ) : null}

      {state === 'error' && !dashboard ? (
        <SecondaryButton label={t('home.retry')} onPress={() => void refresh()} />
      ) : null}
      <View style={styles.quickActions}>
        <View style={styles.quickAction}>
          <PrimaryButton label={t('home.addTransaction')} onPress={() => router.push('/add')} />
        </View>
        <View style={styles.quickAction}>
          <SecondaryButton label={t('home.uploadReceipt')} onPress={() => router.push('/add')} />
        </View>
      </View>
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
  quickActions: { flexDirection: 'row', flexWrap: 'wrap', gap: theme.spacing.sm },
  quickAction: { minWidth: 148, flex: 1 },
});
