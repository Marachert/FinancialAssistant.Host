import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { friendlyApiError } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useInsights } from '@/features/insights/InsightsProvider';
import type { NotificationItem } from '@/features/insights/insightsTypes';
import { formatDateTime, useLocalization } from '@/localization/localization';
import {
  LinkButton,
  LoadingSkeleton,
  ScreenScaffold,
  SecondaryButton,
  StatusBanner,
} from '@/shared/ui';

export default function NotificationsScreen() {
  const { api, profile } = useInsights();
  const { locale, t } = useLocalization(profile?.locale);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [markingId, setMarkingId] = useState<string | null>(null);
  const latestRequest = useRef(0);

  const load = useCallback(async (initial = false) => {
    if (!profile) return;
    const requestId = latestRequest.current + 1;
    latestRequest.current = requestId;
    if (initial) setLoading(true);
    else setRefreshing(true);
    setError(null);

    try {
      const response = await api.getNotifications(profile.currencyCode);
      if (requestId === latestRequest.current) setItems(response.items);
    } catch (reason) {
      if (requestId === latestRequest.current) {
        setError(friendlyApiError(reason, t('notifications.error')));
      }
    } finally {
      if (requestId === latestRequest.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [api, profile, t]);

  useEffect(() => {
    const initialLoad = setTimeout(() => void load(true), 0);
    return () => {
      clearTimeout(initialLoad);
      latestRequest.current += 1;
    };
  }, [load]);

  const markRead = async (notificationId: string) => {
    setMarkingId(notificationId);
    setError(null);
    try {
      const updated = await api.markNotificationRead(notificationId);
      setItems((current) => current.map((item) => (
        item.notificationId === notificationId ? updated : item
      )));
    } catch (reason) {
      setError(friendlyApiError(reason, t('notifications.readError')));
    } finally {
      setMarkingId(null);
    }
  };

  const unreadCount = items.filter((item) => item.readAtUtc === null).length;

  return (
    <ScreenScaffold
      centered={false}
      refreshing={refreshing}
      onRefresh={() => void load()}
    >
      <View style={styles.header}>
        <View style={styles.headerCopy}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>{t('notifications.title')}</Text>
          <Text style={[typography.small, styles.supporting]}>
            {t('notifications.unreadCount', { count: unreadCount })}
          </Text>
        </View>
        <LinkButton label={t('common.back')} onPress={() => router.back()} />
      </View>

      {error ? <StatusBanner>{error}</StatusBanner> : null}
      {loading ? (
        <LoadingSkeleton label={t('notifications.loading')} rows={3} />
      ) : null}

      {!loading && items.length === 0 ? (
        <StatusBanner tone="info">{t('notifications.empty')}</StatusBanner>
      ) : null}

      {!loading ? items.map((item) => {
        const unread = item.readAtUtc === null;
        return (
          <View
            key={item.notificationId}
            accessibilityLabel={t('notifications.itemLabel', {
              status: unread ? t('notifications.unread') : t('notifications.read'),
              title: item.title,
            })}
            style={[styles.notification, unread && styles.notificationUnread]}
          >
            <View style={styles.notificationHeader}>
              <Text style={[typography.bodyStrong, styles.notificationTitle]}>{item.title}</Text>
              <Text style={[typography.caption, unread ? styles.unread : styles.supporting]}>
                {unread ? t('notifications.unread') : t('notifications.read')}
              </Text>
            </View>
            <Text style={[typography.body, styles.title]}>{item.body}</Text>
            <Text style={[typography.caption, styles.supporting]}>
              {`${item.channel === 'push' ? t('notifications.push') : t('notifications.web')} | ${formatDateTime(item.preparedAtUtc, locale)}`}
            </Text>
            {unread ? (
              <SecondaryButton
                label={markingId === item.notificationId ? t('notifications.markingRead') : t('notifications.markRead')}
                disabled={markingId !== null}
                onPress={() => void markRead(item.notificationId)}
              />
            ) : null}
          </View>
        );
      }) : null}

      {error ? (
        <SecondaryButton label={t('notifications.retry')} onPress={() => void load(true)} />
      ) : null}
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { minHeight: 52, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: theme.spacing.md },
  headerCopy: { flex: 1 },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  unread: { color: theme.colors.info, fontWeight: '600' },
  notification: { minHeight: 150, gap: theme.spacing.sm, paddingVertical: theme.spacing.lg, paddingLeft: theme.spacing.md, borderBottomWidth: 1, borderColor: theme.colors.border },
  notificationUnread: { borderLeftWidth: 4, borderLeftColor: theme.colors.info },
  notificationHeader: { minHeight: 24, flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: theme.spacing.md },
  notificationTitle: { flex: 1, color: theme.colors.textPrimary },
});
