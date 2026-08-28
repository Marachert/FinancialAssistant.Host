import type { RequestOptions } from '@/api/client';

import type {
  AnalyticsCategoryBreakdown,
  AnalyticsDashboard,
  AnalyticsPeriod,
  FinancialScore,
  FinancialScoreHistory,
  NotificationItem,
  NotificationList,
  NotificationPreferences,
  ProfileUpdate,
  Recommendation,
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

    getScoreHistory: (currency: string, limit = 12) =>
      request<FinancialScoreHistory>(
        `/financial-score/history?currency=${query(currency)}&limit=${limit}`,
      ),

    getRecommendations: (currency: string) =>
      request<RecommendationList>(`/recommendations?currency=${query(currency)}`),

    markRecommendationRead: (recommendationId: string) =>
      request<Recommendation>(`/recommendations/${query(recommendationId)}/read`, {
        method: 'PUT',
        body: JSON.stringify({ changedAtUtc: new Date().toISOString() }),
      }),

    dismissRecommendation: (recommendationId: string) =>
      request<Recommendation>(`/recommendations/${query(recommendationId)}/dismissal`, {
        method: 'PUT',
        body: JSON.stringify({ changedAtUtc: new Date().toISOString() }),
      }),

    getNotificationPreferences: () =>
      request<NotificationPreferences>('/notification-preferences'),

    updateNotificationPreferences: (preferences: NotificationPreferences) =>
      request<NotificationPreferences>('/notification-preferences', {
        method: 'PUT',
        body: JSON.stringify(preferences),
      }),

    getNotifications: (currency: string) =>
      request<NotificationList>(`/notifications?currency=${query(currency)}`),

    markNotificationRead: (notificationId: string) =>
      request<NotificationItem>(
        `/notifications/${query(notificationId)}/read`,
        {
          method: 'PUT',
          body: JSON.stringify({ changedAtUtc: new Date().toISOString() }),
        },
      ),
  };
}
