import { writable, get } from 'svelte/store';
import { graphqlQuery, graphqlMutation } from '$lib/services/graphql/client';
import { subscribe } from '$lib/services/graphql/subscriptions';
import { showForInAppNotification } from '$lib/services/native-notifications';
import { authStore } from './auth';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import gql from 'graphql-tag';

export interface Notification {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: 'INFO' | 'WARNING' | 'ERROR' | 'SUCCESS';
  link?: string;
  isRead: boolean;
  createdAt: string;
}

interface NotificationState {
  notifications: Notification[];
  /** UTC instant from API; use with createdAt for relative time (same clock as DB). */
  serverTimeUtc: string | null;
  unreadCount: number;
  loading: boolean;
  error: string | null;
}

const initialState: NotificationState = {
  notifications: [],
  serverTimeUtc: null,
  unreadCount: 0,
  loading: false,
  error: null,
};

// Manually defined GraphQL documents since codegen failed
const GET_NOTIFICATIONS = gql`
  query GetNotifications($limit: Int) {
    getNotifications(limit: $limit) {
      notifications {
        id
        userId
        title
        message
        type
        link
        isRead
        createdAt
      }
      serverTimeUtc
    }
  }
`;

const GET_UNREAD_COUNT = gql`
  query GetUnreadNotificationCount {
    unreadCount: getUnreadNotificationCount
  }
`;

const MARK_READ = gql`
  mutation MarkNotificationAsRead($notificationId: UUID!) {
    markNotificationAsRead(notificationId: $notificationId)
  }
`;

const MARK_ALL_READ = gql`
  mutation MarkAllNotificationsAsRead {
    markAllNotificationsAsRead
  }
`;

const ON_NOTIFICATION = gql`
  subscription OnNotification($userId: String!) {
    onNotification(userId: $userId) {
      notification {
        id
        userId
        title
        message
        type
        link
        isRead
        createdAt
      }
      serverTimeUtc
    }
  }
`;

function createNotificationStore() {
  const { subscribe: svelteSubscribe, update, set } = writable<NotificationState>(initialState);
  let unsubscribeSub: (() => void) | null = null;

  async function fetchNotifications(limit = 50) {
    update(s => ({ ...s, loading: true, error: null }));
    const result = await graphqlQuery<{
      getNotifications: { notifications: Notification[]; serverTimeUtc: string };
    }>(GET_NOTIFICATIONS, { variables: { limit }, skipCache: true });

    if (result.success && result.data) {
      const bundle = result.data.getNotifications;
      update(s => ({
        ...s,
        notifications: bundle.notifications,
        serverTimeUtc: bundle.serverTimeUtc,
        loading: false,
      }));
    } else {
      update(s => ({ ...s, error: result.error || 'Failed to fetch notifications', loading: false }));
    }
  }

  async function fetchUnreadCount() {
    const result = await graphqlQuery<{ unreadCount: number }>(GET_UNREAD_COUNT, { skipCache: true });
    if (result.success && result.data) {
      update(s => ({ ...s, unreadCount: result.data!.unreadCount }));
    }
  }

  function setupSubscription() {
    if (unsubscribeSub) unsubscribeSub();

    const user = get(authStore).user;
    if (!user || !user.userId) return;

    unsubscribeSub = subscribe(ON_NOTIFICATION as any, { userId: user.userId }, {
      next: (data: any) => {
        const payload = data.data?.onNotification;
        if (payload?.notification) {
          const newNotif = payload.notification as Notification;
          const serverTimeUtc = payload.serverTimeUtc as string | undefined;
          let toShowNative: Notification | null = null;
          update(s => {
            // Check if notification already exists (avoid duplicates)
            if (s.notifications.some(n => n.id === newNotif.id)) return s;
            toShowNative = newNotif;
            return {
              ...s,
              notifications: [newNotif, ...s.notifications].slice(0, 100),
              serverTimeUtc: serverTimeUtc ?? s.serverTimeUtc,
              unreadCount: s.unreadCount + 1
            };
          });
          if (toShowNative) {
            void showForInAppNotification({
              id: toShowNative.id,
              title: toShowNative.title,
              message: toShowNative.message,
              link: toShowNative.link,
            });
          }
        }
      },
      error: (err) => console.error('Notification subscription error:', err),
      complete: () => {},
    });
  }

  return {
    subscribe: svelteSubscribe,
    async init() {
      await fetchNotifications(50);
      await fetchUnreadCount();
      setupSubscription();
    },
    /** Reload notifications (and unread count). Use a higher limit on the full notifications page. */
    async refresh(limit = 500) {
      await fetchNotifications(limit);
      await fetchUnreadCount();
    },
    async markAsRead(id: string) {
      const result = await graphqlMutation<boolean>(MARK_READ, { variables: { notificationId: id } });
      if (result.success) {
        update(s => ({
          ...s,
          notifications: s.notifications.map(n => n.id === id ? { ...n, isRead: true } : n),
          unreadCount: Math.max(0, s.unreadCount - 1)
        }));
      }
      return result.success;
    },
    async markAllAsRead() {
      const result = await graphqlMutation<boolean>(MARK_ALL_READ);
      if (result.success) {
        update(s => ({
          ...s,
          notifications: s.notifications.map(n => ({ ...n, isRead: true })),
          unreadCount: 0
        }));
      }
      return result.success;
    },
    clear() {
      if (unsubscribeSub) unsubscribeSub();
      set(initialState);
    }
  };
}

export const notificationStore = createNotificationStore();
