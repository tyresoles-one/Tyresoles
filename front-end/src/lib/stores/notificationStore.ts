import { writable, get } from 'svelte/store';
import { graphqlQuery, graphqlMutation } from '$lib/services/graphql/client';
import { subscribe } from '$lib/services/graphql/subscriptions';
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
  unreadCount: number;
  loading: boolean;
  error: string | null;
}

const initialState: NotificationState = {
  notifications: [],
  unreadCount: 0,
  loading: false,
  error: null,
};

// Manually defined GraphQL documents since codegen failed
const GET_NOTIFICATIONS = gql`
  query GetNotifications($limit: Int) {
    notifications: getNotifications(limit: $limit) {
      id
      userId
      title
      message
      type
      link
      isRead
      createdAt
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
      id
      userId
      title
      message
      type
      link
      isRead
      createdAt
    }
  }
`;

function createNotificationStore() {
  const { subscribe: svelteSubscribe, update, set } = writable<NotificationState>(initialState);
  let unsubscribeSub: (() => void) | null = null;

  async function fetchNotifications() {
    update(s => ({ ...s, loading: true }));
    const result = await graphqlQuery<{ notifications: Notification[] }>(GET_NOTIFICATIONS, { variables: { limit: 50 }, skipCache: true });
    
    if (result.success && result.data) {
      update(s => ({ ...s, notifications: result.data!.notifications, loading: false }));
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
        if (data.data?.onNotification) {
          const newNotif = data.data.onNotification;
          update(s => {
            // Check if notification already exists (avoid duplicates)
            if (s.notifications.some(n => n.id === newNotif.id)) return s;
            return {
              ...s,
              notifications: [newNotif, ...s.notifications].slice(0, 100),
              unreadCount: s.unreadCount + 1
            };
          });
        }
      },
      error: (err) => console.error('Notification subscription error:', err),
      complete: () => {},
    });
  }

  return {
    subscribe: svelteSubscribe,
    async init() {
      await fetchNotifications();
      await fetchUnreadCount();
      setupSubscription();
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
