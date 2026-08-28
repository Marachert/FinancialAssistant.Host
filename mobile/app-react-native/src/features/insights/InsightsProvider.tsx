import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react';

import { ApiProblem, friendlyApiError } from '@/api/client';
import { useAuth } from '@/features/auth/AuthProvider';

import { createInsightsApi } from './insightsApi';
import type {
  AnalyticsDashboard,
  FinancialScore,
  ProfileUpdate,
  Recommendation,
  UserProfile,
} from './insightsTypes';

type InsightsState = 'loading' | 'ready' | 'error';

type InsightsContextValue = {
  api: ReturnType<typeof createInsightsApi>;
  state: InsightsState;
  refreshing: boolean;
  error: string | null;
  profile: UserProfile | null;
  dashboard: AnalyticsDashboard | null;
  score: FinancialScore | null;
  scoreHistory: FinancialScore[];
  recommendations: Recommendation[];
  refresh: () => Promise<void>;
  saveProfile: (update: ProfileUpdate) => Promise<UserProfile>;
  markRecommendationRead: (recommendationId: string) => Promise<Recommendation>;
  dismissRecommendation: (recommendationId: string) => Promise<Recommendation>;
};

const InsightsContext = createContext<InsightsContextValue | null>(null);

function errorMessage(reason: unknown) {
  if (reason instanceof ApiProblem && reason.status === 501) {
    return 'Insights are not enabled in this environment yet.';
  }
  return friendlyApiError(reason, 'Your financial overview could not be refreshed. Try again.');
}

export function InsightsProvider({ children }: PropsWithChildren) {
  const { request } = useAuth();
  const api = useMemo(() => createInsightsApi(request), [request]);
  const [state, setState] = useState<InsightsState>('loading');
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [dashboard, setDashboard] = useState<AnalyticsDashboard | null>(null);
  const [score, setScore] = useState<FinancialScore | null>(null);
  const [scoreHistory, setScoreHistory] = useState<FinancialScore[]>([]);
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    setState((current) => current === 'ready' ? current : 'loading');
    try {
      const nextProfile = await api.getProfile();
      setProfile(nextProfile);
      const [dashboardResult, scoreResult, historyResult, recommendationsResult] = await Promise.allSettled([
        api.getDashboard(nextProfile.currencyCode, nextProfile.timeZone),
        api.getScore(nextProfile.currencyCode),
        api.getScoreHistory(nextProfile.currencyCode),
        api.getRecommendations(nextProfile.currencyCode),
      ]);
      if (dashboardResult.status === 'fulfilled') setDashboard(dashboardResult.value);
      if (scoreResult.status === 'fulfilled') setScore(scoreResult.value);
      if (historyResult.status === 'fulfilled') setScoreHistory(historyResult.value.items);
      if (recommendationsResult.status === 'fulfilled') {
        setRecommendations(recommendationsResult.value.items);
      }

      const failure = [dashboardResult, scoreResult, historyResult, recommendationsResult]
        .find((result) => result.status === 'rejected');
      if (failure?.status === 'rejected') {
        setError(errorMessage(failure.reason));
        setState('error');
      } else {
        setState('ready');
      }
    } catch (reason) {
      setError(errorMessage(reason));
      setState('error');
    } finally {
      setRefreshing(false);
    }
  }, [api]);

  const saveProfile = useCallback(async (update: ProfileUpdate) => {
    const savedProfile = await api.updateProfile(update);
    setProfile(savedProfile);
    return savedProfile;
  }, [api]);

  const markRecommendationRead = useCallback(async (recommendationId: string) => {
    const updated = await api.markRecommendationRead(recommendationId);
    setRecommendations((current) => current.map((item) => (
      item.recommendationId === updated.recommendationId ? updated : item
    )));
    return updated;
  }, [api]);

  const dismissRecommendation = useCallback(async (recommendationId: string) => {
    const updated = await api.dismissRecommendation(recommendationId);
    setRecommendations((current) => current.map((item) => (
      item.recommendationId === updated.recommendationId ? updated : item
    )));
    return updated;
  }, [api]);

  useEffect(() => {
    const initialRefresh = setTimeout(() => void refresh(), 0);
    return () => clearTimeout(initialRefresh);
  }, [refresh]);

  const value = useMemo<InsightsContextValue>(
    () => ({
      api,
      state,
      refreshing,
      error,
      profile,
      dashboard,
      score,
      scoreHistory,
      recommendations,
      refresh,
      saveProfile,
      markRecommendationRead,
      dismissRecommendation,
    }),
    [api, dashboard, dismissRecommendation, error, markRecommendationRead, profile, recommendations, refresh, refreshing, saveProfile, score, scoreHistory, state],
  );

  return <InsightsContext.Provider value={value}>{children}</InsightsContext.Provider>;
}

export function useInsights(): InsightsContextValue {
  const context = useContext(InsightsContext);
  if (!context) throw new Error('useInsights must be used within InsightsProvider.');
  return context;
}
