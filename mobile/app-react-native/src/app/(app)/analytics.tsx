import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { friendlyApiError } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import type {
  AnalyticsCategoryBreakdown,
  AnalyticsCategoryBreakdownItem,
  AnalyticsDashboard,
  AnalyticsPeriod,
} from '@/features/insights/insightsTypes';
import { formatCurrency, formatDateOnly, useLocalization } from '@/localization/localization';
import {
  LinkButton,
  LoadingSkeleton,
  ScreenScaffold,
  SecondaryButton,
  SegmentedControl,
  StatusBanner,
} from '@/shared/ui';

type PeriodLabel = 'Daily' | 'Weekly' | 'Monthly';

const periodByLabel: Record<PeriodLabel, AnalyticsPeriod> = {
  Daily: 'daily',
  Weekly: 'weekly',
  Monthly: 'monthly',
};

function categoryName(categoryId: string, uncategorized: string) {
  const label = categoryId.replace(/[-_]+/g, ' ').trim();
  return label ? `${label.charAt(0).toUpperCase()}${label.slice(1)}` : uncategorized;
}

function CategoryBar({
  item,
  currency,
  locale,
  uncategorized,
}: {
  item: AnalyticsCategoryBreakdownItem;
  currency: string;
  locale: string;
  uncategorized: string;
}) {
  const width = `${Math.min(100, Math.max(0, item.expenseSharePercent))}%` as `${number}%`;
  const value = `${formatCurrency(item.expenseTotal, currency, locale)}, ${item.expenseSharePercent.toFixed(1)}%`;
  const name = categoryName(item.categoryId, uncategorized);

  return (
    <View
      accessibilityLabel={`${name}: ${value}`}
      style={styles.categoryRow}
    >
      <View style={styles.categoryLabelRow}>
        <Text numberOfLines={2} style={[typography.bodyStrong, styles.categoryName]}>
          {name}
        </Text>
        <Text
          adjustsFontSizeToFit
          minimumFontScale={0.75}
          numberOfLines={1}
          style={[typography.small, styles.supporting, styles.categoryValue]}
        >
          {value}
        </Text>
      </View>
      <View style={styles.barTrack}>
        <View style={[styles.barValue, { width }]} />
      </View>
    </View>
  );
}

export default function AnalyticsScreen() {
  const { api, profile } = useInsights();
  const { locale, t } = useLocalization(profile?.locale);
  const [periodLabel, setPeriodLabel] = useState<PeriodLabel>('Monthly');
  const [dashboard, setDashboard] = useState<AnalyticsDashboard | null>(null);
  const [breakdown, setBreakdown] = useState<AnalyticsCategoryBreakdown | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const latestRequest = useRef(0);
  const periodOptions = [
    { value: 'Daily', label: t('analytics.daily') },
    { value: 'Weekly', label: t('analytics.weekly') },
    { value: 'Monthly', label: t('analytics.monthly') },
  ] as const;
  const periodDisplay = periodLabel === 'Daily'
    ? t('analytics.daily')
    : periodLabel === 'Weekly'
      ? t('analytics.weekly')
      : t('analytics.monthly');

  const load = useCallback(async (showInitialLoading = false) => {
    if (!profile) return;
    const requestId = latestRequest.current + 1;
    latestRequest.current = requestId;
    if (showInitialLoading) {
      setLoading(true);
      setBreakdown(null);
    }
    else setRefreshing(true);
    setError(null);

    try {
      const [dashboardResult, breakdownResult] = await Promise.allSettled([
        api.getDashboard(profile.currencyCode, profile.timeZone),
        api.getCategoryBreakdown(
          profile.currencyCode,
          profile.timeZone,
          periodByLabel[periodLabel],
        ),
      ]);
      if (requestId === latestRequest.current) {
        if (dashboardResult.status === 'fulfilled') setDashboard(dashboardResult.value);
        if (breakdownResult.status === 'fulfilled') setBreakdown(breakdownResult.value);
        const failure = [dashboardResult, breakdownResult]
          .find((result) => result.status === 'rejected');
        if (failure?.status === 'rejected') {
          setError(friendlyApiError(failure.reason, t('analytics.error')));
        }
      }
    } catch (reason) {
      if (requestId === latestRequest.current) {
        setError(friendlyApiError(reason, t('analytics.error')));
      }
    } finally {
      if (requestId === latestRequest.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [api, periodLabel, profile, t]);

  useEffect(() => {
    const initialLoad = setTimeout(() => void load(true), 0);
    return () => {
      clearTimeout(initialLoad);
      latestRequest.current += 1;
    };
  }, [load]);

  const summary = dashboard
    ? periodLabel === 'Daily'
      ? dashboard.dailySummary
      : periodLabel === 'Weekly'
        ? dashboard.weeklySummary
        : dashboard.monthlySummary
    : null;
  const categories = breakdown?.topExpenseCategories ?? [];
  const currency = dashboard?.currency ?? breakdown?.currency;
  const isStale = Boolean(dashboard?.freshness.isStale || breakdown?.freshness.isStale);
  const hasActivity = Boolean(summary && (summary.incomeTotal !== 0 || summary.expenseTotal !== 0));

  return (
    <ScreenScaffold
      centered={false}
      refreshing={refreshing}
      onRefresh={() => void load()}
    >
      <View style={styles.header}>
        <View style={styles.headerCopy}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>{t('analytics.title')}</Text>
          <Text style={[typography.small, styles.supporting]}>{t('analytics.subtitle')}</Text>
        </View>
        <LinkButton label={t('common.back')} onPress={() => router.back()} />
      </View>

      <SegmentedControl
        label={t('analytics.periodLabel')}
        options={periodOptions}
        value={periodLabel}
        onChange={(value) => setPeriodLabel(value as PeriodLabel)}
      />

      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {isStale ? (
        <StatusBanner tone="warning">{t('analytics.stale')}</StatusBanner>
      ) : null}

      {loading && !dashboard ? (
        <LoadingSkeleton label={t('analytics.loading')} rows={3} />
      ) : null}

      {summary && dashboard ? (
        <>
          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>{t('analytics.summary', { period: periodDisplay })}</Text>
            <View style={styles.metricGrid}>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>{t('home.income')}</Text>
                <Text style={[typography.bodyStrong, styles.positive]}>
                  {formatCurrency(summary.incomeTotal, dashboard.currency, locale)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>{t('analytics.spending')}</Text>
                <Text style={[typography.bodyStrong, styles.title]}>
                  {formatCurrency(summary.expenseTotal, dashboard.currency, locale)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>{t('home.balanceChange')}</Text>
                <Text style={[
                  typography.bodyStrong,
                  summary.balanceDelta >= 0 ? styles.positive : styles.critical,
                ]}>
                  {formatCurrency(summary.balanceDelta, dashboard.currency, locale)}
                </Text>
              </View>
            </View>
            {!hasActivity ? (
              <StatusBanner tone="info">{t('analytics.noActivity')}</StatusBanner>
            ) : null}
          </View>

        </>
      ) : null}

      <View style={styles.section}>
        <View style={styles.sectionCopy}>
          <Text style={[typography.heading, styles.title]}>{t('analytics.byCategory')}</Text>
          {breakdown ? (
            <Text style={[typography.small, styles.supporting]}>
              {`${formatDateOnly(breakdown.periodStart, locale)} – ${formatDateOnly(breakdown.periodEnd, locale)}`}
            </Text>
          ) : null}
        </View>
        {loading && !breakdown ? (
          <LoadingSkeleton label={t('analytics.loadingCategories')} rows={2} />
        ) : categories.length && currency ? (
          <View accessibilityRole="summary" style={styles.chart}>
            {categories.map((item) => (
              <CategoryBar
                key={item.categoryId}
                item={item}
                currency={currency}
                locale={locale}
                uncategorized={t('analytics.uncategorized')}
              />
            ))}
          </View>
        ) : breakdown ? (
          <StatusBanner tone="info">{t('analytics.noCategories')}</StatusBanner>
        ) : null}
      </View>

      {error ? (
        <SecondaryButton label={t('analytics.retry')} onPress={() => void load(true)} />
      ) : null}
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
  section: { gap: theme.spacing.md, paddingVertical: theme.spacing.sm },
  sectionCopy: { gap: theme.spacing.xs },
  metricGrid: { flexDirection: 'row', flexWrap: 'wrap', borderTopWidth: 1, borderColor: theme.colors.border },
  metric: { width: '50%', minHeight: 76, gap: theme.spacing.xs, paddingVertical: theme.spacing.md, paddingRight: theme.spacing.sm, borderBottomWidth: 1, borderColor: theme.colors.border },
  chart: { gap: theme.spacing.lg },
  categoryRow: { minHeight: 58, gap: theme.spacing.sm },
  categoryLabelRow: { minHeight: 24, flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: theme.spacing.md },
  categoryName: { flex: 1, color: theme.colors.textPrimary },
  categoryValue: { maxWidth: '58%', flexShrink: 1, textAlign: 'right' },
  barTrack: { height: 12, overflow: 'hidden', borderRadius: theme.radius.control, backgroundColor: theme.colors.surfaceSubtle },
  barValue: { height: 12, backgroundColor: theme.colors.info },
});
