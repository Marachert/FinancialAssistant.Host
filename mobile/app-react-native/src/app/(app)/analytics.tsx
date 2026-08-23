import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import type {
  AnalyticsCategoryBreakdown,
  AnalyticsCategoryBreakdownItem,
  AnalyticsDashboard,
  AnalyticsPeriod,
} from '@/features/insights/insightsTypes';
import {
  LinkButton,
  ScreenScaffold,
  SecondaryButton,
  SegmentedControl,
  StatusBanner,
} from '@/shared/ui';

const periodLabels = ['Daily', 'Weekly', 'Monthly'] as const;
type PeriodLabel = (typeof periodLabels)[number];

const periodByLabel: Record<PeriodLabel, AnalyticsPeriod> = {
  Daily: 'daily',
  Weekly: 'weekly',
  Monthly: 'monthly',
};

function money(value: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(value);
}

function categoryName(categoryId: string) {
  const label = categoryId.replace(/[-_]+/g, ' ').trim();
  return label ? `${label.charAt(0).toUpperCase()}${label.slice(1)}` : 'Uncategorized';
}

function errorMessage(reason: unknown) {
  return reason instanceof Error
    ? reason.message
    : 'Analytics could not be loaded. Try again.';
}

function CategoryBar({ item, currency }: { item: AnalyticsCategoryBreakdownItem; currency: string }) {
  const width = `${Math.min(100, Math.max(0, item.expenseSharePercent))}%` as `${number}%`;
  const value = `${money(item.expenseTotal, currency)}, ${item.expenseSharePercent.toFixed(1)}%`;

  return (
    <View
      accessibilityLabel={`${categoryName(item.categoryId)}: ${value}`}
      style={styles.categoryRow}
    >
      <View style={styles.categoryLabelRow}>
        <Text numberOfLines={2} style={[typography.bodyStrong, styles.categoryName]}>
          {categoryName(item.categoryId)}
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
  const [periodLabel, setPeriodLabel] = useState<PeriodLabel>('Monthly');
  const [dashboard, setDashboard] = useState<AnalyticsDashboard | null>(null);
  const [breakdown, setBreakdown] = useState<AnalyticsCategoryBreakdown | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const latestRequest = useRef(0);

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
        if (failure?.status === 'rejected') setError(errorMessage(failure.reason));
      }
    } catch (reason) {
      if (requestId === latestRequest.current) setError(errorMessage(reason));
    } finally {
      if (requestId === latestRequest.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [api, periodLabel, profile]);

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

  return (
    <ScreenScaffold
      centered={false}
      refreshing={refreshing}
      onRefresh={() => void load()}
    >
      <View style={styles.header}>
        <View style={styles.headerCopy}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>Analytics</Text>
          <Text style={[typography.small, styles.supporting]}>Income and confirmed spending</Text>
        </View>
        <LinkButton label="Back" onPress={() => router.back()} />
      </View>

      <SegmentedControl
        label="Analytics period"
        options={periodLabels}
        value={periodLabel}
        onChange={(value) => setPeriodLabel(value as PeriodLabel)}
      />

      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {isStale ? (
        <StatusBanner tone="warning">Some analytics are still catching up. Last known values are shown.</StatusBanner>
      ) : null}

      {loading && !dashboard ? (
        <View accessibilityLiveRegion="polite" style={styles.loading}>
          <ActivityIndicator color={theme.colors.action} />
          <Text style={[typography.body, styles.supporting]}>Loading analytics...</Text>
        </View>
      ) : null}

      {summary && dashboard ? (
        <>
          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>{periodLabel} summary</Text>
            <View style={styles.metricGrid}>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>Income</Text>
                <Text style={[typography.bodyStrong, styles.positive]}>
                  {money(summary.incomeTotal, dashboard.currency)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>Spending</Text>
                <Text style={[typography.bodyStrong, styles.title]}>
                  {money(summary.expenseTotal, dashboard.currency)}
                </Text>
              </View>
              <View style={styles.metric}>
                <Text style={[typography.caption, styles.supporting]}>Balance change</Text>
                <Text style={[
                  typography.bodyStrong,
                  summary.balanceDelta >= 0 ? styles.positive : styles.critical,
                ]}>
                  {money(summary.balanceDelta, dashboard.currency)}
                </Text>
              </View>
            </View>
          </View>

        </>
      ) : null}

      <View style={styles.section}>
        <View style={styles.sectionCopy}>
          <Text style={[typography.heading, styles.title]}>Spending by category</Text>
          {breakdown ? (
            <Text style={[typography.small, styles.supporting]}>
              {`${breakdown.periodStart} to ${breakdown.periodEnd}`}
            </Text>
          ) : null}
        </View>
        {loading && !breakdown ? (
          <ActivityIndicator accessibilityLabel="Loading categories" color={theme.colors.action} />
        ) : categories.length && currency ? (
          <View accessibilityRole="summary" style={styles.chart}>
            {categories.map((item) => (
              <CategoryBar key={item.categoryId} item={item} currency={currency} />
            ))}
          </View>
        ) : breakdown ? (
          <StatusBanner tone="info">No spending categories for this period yet.</StatusBanner>
        ) : null}
      </View>

      {error ? (
        <SecondaryButton label="Retry analytics" onPress={() => void load(true)} />
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
  loading: { minHeight: 180, alignItems: 'center', justifyContent: 'center', gap: theme.spacing.md },
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
