import { router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import { LinkButton, LoadingSkeleton, ScreenScaffold, SecondaryButton, StatusBanner } from '@/shared/ui';

function label(code: string) {
  return code.replaceAll('_', ' ').replaceAll('-', ' ');
}

export default function ScoreScreen() {
  const { state, refreshing, error, score, refresh } = useInsights();
  const width = `${Math.min(100, Math.max(0, score?.score ?? 0))}%` as `${number}%`;

  return (
    <ScreenScaffold centered={false} refreshing={refreshing} onRefresh={() => void refresh()}>
      <View style={styles.header}>
        <LinkButton label="Back" onPress={() => router.back()} />
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Financial score</Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {state === 'loading' && !score ? (
        <LoadingSkeleton label="Loading financial score" rows={3} />
      ) : null}
      {score ? (
        <>
          <View style={styles.summary}>
            <Text style={[typography.display, styles.title]}>{score.score}</Text>
            <Text style={[typography.body, styles.supporting]}>out of 100</Text>
            <View
              accessibilityRole="progressbar"
              accessibilityValue={{ min: 0, max: 100, now: score.score }}
              style={styles.progressTrack}
            >
              <View style={[styles.progressValue, { width }]} />
            </View>
            <Text style={[typography.small, styles.supporting]}>
              Calculated from confirmed financial activity
            </Text>
          </View>
          <View style={styles.factors}>
            <Text style={[typography.heading, styles.title]}>What affects your score</Text>
            {score.factors.length ? score.factors.map((factor) => (
              <View key={factor.code} style={styles.factor}>
                <View style={styles.factorHeader}>
                  <Text style={[typography.bodyStrong, styles.factorName]}>{label(factor.code)}</Text>
                  <Text style={[typography.bodyStrong, factor.contribution >= 0 ? styles.positive : styles.critical]}>
                    {factor.contribution >= 0 ? '+' : ''}{factor.contribution}
                  </Text>
                </View>
                <Text style={[typography.body, styles.supporting]}>{factor.explanation}</Text>
                {factor.inputs.map((input) => (
                  <Text key={input.code} style={[typography.caption, styles.supporting]}>
                    {label(input.code)}: {input.value} {input.unit}
                  </Text>
                ))}
              </View>
            )) : (
              <Text style={[typography.body, styles.supporting]}>Score factors will appear after confirmed activity is processed.</Text>
            )}
          </View>
        </>
      ) : null}
      {state === 'error' && !score ? (
        <SecondaryButton label="Retry score" onPress={() => void refresh()} />
      ) : null}
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { minHeight: 48, flexDirection: 'row', alignItems: 'center', gap: theme.spacing.md },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  positive: { color: theme.colors.positive },
  critical: { color: theme.colors.critical },
  summary: { gap: theme.spacing.sm, paddingVertical: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  progressTrack: { height: 10, overflow: 'hidden', borderRadius: theme.radius.control, backgroundColor: theme.colors.surfaceSubtle },
  progressValue: { height: 10, backgroundColor: theme.colors.action },
  factors: { gap: theme.spacing.md },
  factor: { gap: theme.spacing.sm, paddingVertical: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  factorHeader: { minHeight: 28, flexDirection: 'row', justifyContent: 'space-between', gap: theme.spacing.md },
  factorName: { flex: 1, color: theme.colors.textPrimary, textTransform: 'capitalize' },
});
