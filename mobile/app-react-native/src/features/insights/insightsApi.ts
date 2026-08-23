import type { RequestOptions } from '@/api/client';

import type {
  AnalyticsCategoryBreakdown,
  AnalyticsDashboard,
  AnalyticsPeriod,
  FinancialScore,
  NotificationPreferences,
  ProfileUpdate,
  RecommendationList,
  UserProfile,
} from './insightsTypes';

type Request = <T>(path: string, options?: RequestOptions) => Promise<T>;

function query(value: string) {
  return encodeURIComponent(value);
}

export function createInsightsApi(request: Request) {
  return {
    getProfile: () => request<UserProfile>('/users/me'),

    updateProfile: (update: ProfileUpdate) =>
      request<UserProfile>('/users/me/preferences', {
        method: 'PUT',
        body: JSON.stringify(update),
      }),

    getDashboard: (currency: string, timeZone: string) =>
      request<AnalyticsDashboard>(
        `/analytics/dashboard?currency=${query(currency)}&timeZoneId=${query(timeZone)}&trendDays=7`,
      ),

    getCategoryBreakdown: (currency: string, timeZone: string, period: AnalyticsPeriod) =>
      request<AnalyticsCategoryBreakdown>(
        `/analytics/category-breakdown?currency=${query(currency)}&timeZoneId=${query(timeZone)}&period=${period}&top=5`,
      ),

    getScore: (currency: string) =>
      request<FinancialScore>(`/financial-score/current?currency=${query(currency)}`),

    getRecommendations: (currency: string) =>
      request<RecommendationList>(`/recommendations?currency=${query(currency)}`),

    getNotificationPreferences: () =>
      request<NotificationPreferences>('/notification-preferences'),

    updateNotificationPreferences: (preferences: NotificationPreferences) =>
      request<NotificationPreferences>('/notification-preferences', {
        method: 'PUT',
        body: JSON.stringify(preferences),
      }),
  };
}
