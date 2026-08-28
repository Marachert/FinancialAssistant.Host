import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { friendlyApiError } from '@/api/client';
import { useInsights } from '@/features/insights/InsightsProvider';
import { LinkButton, LoadingSkeleton, PrimaryButton, ScreenScaffold, SecondaryButton, StatusBanner } from '@/shared/ui';

type AppRoute = '/home' | '/score' | '/analytics' | '/settings' | '/add';

const actionRoutes: Record<string, AppRoute> = {
  'view-dashboard': '/home',
  'view-progress': '/home',
  'review-score': '/score',
  'review-categories': '/analytics',
  'review-cash-flow': '/analytics',
  'complete-profile': '/settings',
  'review-limits': '/settings',
  'review-expenses': '/add',
  'categorize-expenses': '/add',
  'add-income': '/add',
};

function label(code: string) {
  return code.replaceAll('_', ' ').replaceAll('-', ' ');
}

export default function RecommendationDetailScreen() {
  const { recommendationId } = useLocalSearchParams<{ recommendationId: string }>();
  const {
    state,
    refreshing,
    error,
    recommendations,
    refresh,
    markRecommendationRead,
    dismissRecommendation,
  } = useInsights();
  const [action, setAction] = useState<'read' | 'dismiss' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const recommendation = recommendations.find((item) => item.recommendationId === recommendationId);
  const suggestedRoute = recommendation ? actionRoutes[recommendation.explanation.action.code] : undefined;
  const canRead = recommendation?.status === 'active';
  const canDismiss = recommendation?.status === 'active' || recommendation?.status === 'read';

  const updateStatus = async (nextAction: 'read' | 'dismiss') => {
    if (!recommendation) return;
    setAction(nextAction);
    setActionError(null);
    setActionSuccess(null);
    try {
      if (nextAction === 'read') {
        await markRecommendationRead(recommendation.recommendationId);
        setActionSuccess('Recommendation marked as read.');
      } else {
        await dismissRecommendation(recommendation.recommendationId);
        setActionSuccess('Recommendation dismissed.');
      }
    } catch (reason) {
      setActionError(friendlyApiError(reason, 'The recommendation could not be updated. Try again.'));
    } finally {
      setAction(null);
    }
  };

  return (
    <ScreenScaffold centered={false} refreshing={refreshing} onRefresh={() => void refresh()}>
      <View style={styles.header}>
        <LinkButton label="Back" onPress={() => router.back()} />
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Recommendation</Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {actionError ? <StatusBanner>{actionError}</StatusBanner> : null}
      {actionSuccess ? <StatusBanner tone="success">{actionSuccess}</StatusBanner> : null}
      {state === 'loading' && !recommendation ? (
        <LoadingSkeleton label="Loading recommendation detail" rows={4} />
      ) : null}
      {state !== 'loading' && !recommendation ? (
        <View style={styles.empty}>
          <Text style={[typography.heading, styles.title]}>Recommendation unavailable</Text>
          <Text style={[typography.body, styles.supporting]}>
            It may have expired or been replaced after your financial picture changed.
          </Text>
          <SecondaryButton label="Refresh recommendations" onPress={() => void refresh()} />
        </View>
      ) : null}
      {recommendation ? (
        <>
          <View style={styles.summary}>
            <View style={styles.meta}>
              <Text style={[typography.caption, styles.severity]}>{recommendation.severity}</Text>
              <Text style={[typography.caption, styles.status]}>{recommendation.status}</Text>
            </View>
            <Text style={[typography.heading, styles.title]}>{recommendation.title}</Text>
            <Text style={[typography.body, styles.title]}>{recommendation.body}</Text>
          </View>
          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>Why you are seeing this</Text>
            <Text style={[typography.body, styles.supporting]}>{recommendation.explanation.text}</Text>
            <Text style={[typography.caption, styles.supporting]}>
              Evidence confidence: {recommendation.explanation.confidence}
            </Text>
            {recommendation.facts.map((fact) => (
              <View key={fact.code} style={styles.fact}>
                <Text style={[typography.small, styles.factLabel]}>{label(fact.code)}</Text>
                <Text style={[typography.bodyStrong, styles.title]}>{fact.value}</Text>
              </View>
            ))}
          </View>
          <View style={styles.section}>
            <Text style={[typography.heading, styles.title]}>Suggested next step</Text>
            <Text style={[typography.body, styles.supporting]}>
              {label(recommendation.explanation.action.code)}
            </Text>
            {suggestedRoute ? (
              <PrimaryButton label="Open suggested action" onPress={() => router.push(suggestedRoute)} />
            ) : (
              <StatusBanner tone="info">This action is not available in the current app version.</StatusBanner>
            )}
          </View>
          <View style={styles.actions}>
            {canRead ? (
              <PrimaryButton
                label="Mark as read"
                loading={action === 'read'}
                disabled={action !== null}
                onPress={() => void updateStatus('read')}
              />
            ) : null}
            {canDismiss ? (
              <SecondaryButton
                label="Dismiss recommendation"
                disabled={action !== null}
                onPress={() => void updateStatus('dismiss')}
              />
            ) : null}
            {!canRead && !canDismiss ? (
              <StatusBanner tone="info">This recommendation is {recommendation.status}.</StatusBanner>
            ) : null}
          </View>
        </>
      ) : null}
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { minHeight: 48, flexDirection: 'row', alignItems: 'center', gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  empty: { minHeight: 200, justifyContent: 'center', gap: theme.spacing.md },
  summary: { gap: theme.spacing.md, paddingBottom: theme.spacing.lg, borderBottomWidth: 1, borderColor: theme.colors.border },
  meta: { minHeight: 28, flexDirection: 'row', justifyContent: 'space-between', gap: theme.spacing.md },
  severity: { color: theme.colors.info, textTransform: 'uppercase' },
  status: { color: theme.colors.textSecondary, textTransform: 'capitalize' },
  section: { gap: theme.spacing.md, paddingVertical: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  fact: { minHeight: 32, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: theme.spacing.md },
  factLabel: { flex: 1, color: theme.colors.textSecondary, textTransform: 'capitalize' },
  actions: { gap: theme.spacing.md },
});
