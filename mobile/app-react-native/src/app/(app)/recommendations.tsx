import { router } from 'expo-router';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import { LinkButton, ScreenScaffold, SecondaryButton, StatusBanner } from '@/shared/ui';

export default function RecommendationsScreen() {
  const { state, refreshing, error, recommendations, refresh } = useInsights();

  return (
    <ScreenScaffold centered={false} refreshing={refreshing} onRefresh={() => void refresh()}>
      <View style={styles.header}>
        <LinkButton label="Back" onPress={() => router.back()} />
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Recommendations</Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {state === 'loading' && !recommendations.length ? (
        <View style={styles.loading}>
          <ActivityIndicator color={theme.colors.action} />
          <Text style={[typography.body, styles.supporting]}>Loading recommendations...</Text>
        </View>
      ) : null}
      {state !== 'loading' && !recommendations.length ? (
        <View style={styles.empty}>
          <Text style={[typography.heading, styles.title]}>You are caught up</Text>
          <Text style={[typography.body, styles.supporting]}>New guidance will appear when your confirmed financial picture changes.</Text>
        </View>
      ) : null}
      <View style={styles.list}>
        {recommendations.map((recommendation) => (
          <View key={recommendation.recommendationId} style={styles.item}>
            <View style={styles.itemHeader}>
              <Text style={[typography.bodyStrong, styles.itemTitle]}>{recommendation.title}</Text>
              <Text style={[typography.caption, styles.severity]}>{recommendation.severity}</Text>
            </View>
            <Text style={[typography.body, styles.title]}>{recommendation.body}</Text>
            <Text style={[typography.small, styles.supporting]}>{recommendation.explanation.text}</Text>
            <Text style={[typography.caption, styles.supporting]}>
              Confidence: {recommendation.explanation.confidence}
            </Text>
            {recommendation.facts.map((fact) => (
              <Text key={fact.code} style={[typography.caption, styles.supporting]}>
                {fact.code.replaceAll('_', ' ')}: {fact.value}
              </Text>
            ))}
          </View>
        ))}
      </View>
      {state === 'error' && !recommendations.length ? (
        <SecondaryButton label="Retry recommendations" onPress={() => void refresh()} />
      ) : null}
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { minHeight: 48, flexDirection: 'row', alignItems: 'center', gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  loading: { minHeight: 160, alignItems: 'center', justifyContent: 'center', gap: theme.spacing.md },
  empty: { minHeight: 160, justifyContent: 'center', gap: theme.spacing.sm, paddingVertical: theme.spacing.xl },
  list: { gap: theme.spacing.md },
  item: { gap: theme.spacing.sm, padding: theme.spacing.md, borderWidth: 1, borderColor: theme.colors.border, borderRadius: theme.radius.control, backgroundColor: theme.colors.surface },
  itemHeader: { minHeight: 28, flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: theme.spacing.md },
  itemTitle: { flex: 1, color: theme.colors.textPrimary },
  severity: { color: theme.colors.info, textTransform: 'uppercase' },
});
