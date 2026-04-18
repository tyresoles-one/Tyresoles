/**
 * OS-level notifications for in-app GraphQL notifications.
 * - Browser: Web Notifications API (HTTPS / localhost; iOS has limited support).
 * - Tauri: @tauri-apps/plugin-notification after permission.
 * - Capacitor: @capacitor/local-notifications.
 */

import { isTauri } from '$lib/tauri';
import { isCapacitor } from '$lib/utils/native-token-store';

const STORAGE_KEY = 'tyresoles_nativeAlertsEnabled';

/** When true, only show native toast if the document tab is in the background (avoids duplicate with in-app bell). */
const ONLY_WHEN_TAB_NOT_VISIBLE = false;

// Track if capacitor listener is registered
let _capacitorListenerRegistered = false;

export function isNativeAlertsEnabled(): boolean {
  if (typeof localStorage === 'undefined') return false;
  return localStorage.getItem(STORAGE_KEY) === '1';
}

export function setNativeAlertsEnabled(enabled: boolean): void {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, enabled ? '1' : '0');
}

export function isWebNotificationSupported(): boolean {
  return typeof window !== 'undefined' && 'Notification' in window;
}

/**
 * Current permission without prompting. 'default' means not decided yet.
 */
export async function getWebNotificationPermission(): Promise<NotificationPermission | 'unsupported'> {
  if (isCapacitor()) {
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      const status = await LocalNotifications.checkPermissions();
      return status.display === 'granted' ? 'granted' : status.display === 'denied' ? 'denied' : 'default';
    } catch {
      return 'unsupported';
    }
  }
  if (!isWebNotificationSupported()) return 'unsupported';
  return Notification.permission;
}

/** Human-readable permission line for Settings UI. */
export async function getNativePermissionLabel(): Promise<string> {
  if (isTauri()) {
    try {
      const { isPermissionGranted } = await import('@tauri-apps/plugin-notification');
      return (await isPermissionGranted()) ? 'Granted' : 'Not granted';
    } catch {
      return 'Unknown';
    }
  }
  if (isCapacitor()) {
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      const status = await LocalNotifications.checkPermissions();
      return status.display === 'granted' ? 'Granted' : 'Not granted';
    } catch {
      return 'Unknown';
    }
  }
  const p = await getWebNotificationPermission();
  return p === 'unsupported' ? 'Not supported in this browser' : p;
}

/**
 * Request OS permission for notifications.
 */
export async function requestNativeNotificationPermission(): Promise<
  'granted' | 'denied' | 'default' | 'unsupported'
> {
  if (isTauri()) {
    try {
      const { isPermissionGranted, requestPermission } = await import('@tauri-apps/plugin-notification');
      if (await isPermissionGranted()) return 'granted';
      const result = await requestPermission();
      return result === 'granted' ? 'granted' : 'denied';
    } catch {
      return 'denied';
    }
  }
  if (isCapacitor()) {
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      const status = await LocalNotifications.requestPermissions();
      return status.display === 'granted' ? 'granted' : 'denied';
    } catch {
      return 'denied';
    }
  }
  if (!isWebNotificationSupported()) return 'unsupported';
  const r = await Notification.requestPermission();
  return r === 'granted' ? 'granted' : r === 'denied' ? 'denied' : 'default';
}

async function hasNativePermission(): Promise<boolean> {
  if (isTauri()) {
    try {
      const { isPermissionGranted } = await import('@tauri-apps/plugin-notification');
      return await isPermissionGranted();
    } catch {
      return false;
    }
  }
  if (isCapacitor()) {
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      const status = await LocalNotifications.checkPermissions();
      return status.display === 'granted';
    } catch {
      return false;
    }
  }
  if (!isWebNotificationSupported()) return false;
  return Notification.permission === 'granted';
}

function shouldShowNativeToast(): boolean {
  if (!isNativeAlertsEnabled()) return false;
  if (typeof document === 'undefined') return true;
  // Capacitor local notifications handle their own display logic but we might still want to suppress
  if (ONLY_WHEN_TAB_NOT_VISIBLE && document.visibilityState === 'visible' && !isCapacitor()) return false;
  return true;
}

async function navigateToLink(link: string | undefined): Promise<void> {
  if (!link) return;
  try {
    const url = new URL(link, window.location.origin);
    if (url.origin === window.location.origin) {
      const { goto } = await import('$app/navigation');
      await goto(url.pathname + url.search + url.hash);
    } else {
      window.open(link, '_blank', 'noopener,noreferrer');
    }
  } catch {
    // ignore malformed URLs
  }
}

async function focusAppWindow(): Promise<void> {
  if (isTauri()) {
    try {
      const { getCurrentWindow } = await import('@tauri-apps/api/window');
      await getCurrentWindow().setFocus();
      return;
    } catch {
      // fall through
    }
  }
  if (isCapacitor()) {
    // Background to foreground happens visually when they click the local notification.
    return;
  }
  if (typeof window !== 'undefined') window.focus();
}

/**
 * Ensures capacitor click listeners handles routing.
 */
async function registerCapacitorListenerIfNeeded() {
  if (!isCapacitor() || _capacitorListenerRegistered) return;
  try {
    const { LocalNotifications } = await import('@capacitor/local-notifications');
    await LocalNotifications.addListener('localNotificationActionPerformed', (notificationAction) => {
      const extra = notificationAction.notification.extra;
      void (async () => {
        await focusAppWindow();
        if (extra?.link) await navigateToLink(extra.link);
      })();
    });
    _capacitorListenerRegistered = true;
  } catch (e) {
    console.warn('Could not register Capacitor listener', e);
  }
}

function hashCode(str: string) {
  let hash = 0, i, chr;
  if (str.length === 0) return hash;
  for (i = 0; i < str.length; i++) {
    chr = str.charCodeAt(i);
    hash = ((hash << 5) - hash) + chr;
    hash |= 0;
  }
  return hash;
}

/**
 * Show a native notification for a new in-app notification (subscription payload).
 */
export async function showForInAppNotification(n: {
  id: string;
  title: string;
  message: string;
  link?: string;
}): Promise<void> {
  if (!shouldShowNativeToast()) return;
  const ok = await hasNativePermission();
  if (!ok) return;

  const title = n.title?.trim() || 'Tyresoles';
  const body = n.message?.trim() || '';

  if (isCapacitor()) {
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      await registerCapacitorListenerIfNeeded();
      
      // Use purely numeric ID for capacitor local notifications
      let capId = 1;
      try {
        capId = Math.abs(hashCode(n.id));
      } catch {}

      await LocalNotifications.schedule({
        notifications: [
          {
            title,
            body,
            id: capId,
            extra: { link: n.link ?? null }
          }
        ]
      });
      return;
    } catch (e) {
      console.warn('Capacitor LocalNotification failed', e);
    }
  }

  /*
   * Browser and Tauri both use the Web Notifications API in the webview (the plugin’s
   * sendNotification is a thin wrapper around `new Notification`).
   */
  if (!isWebNotificationSupported()) return;

  try {
    const icon = `${typeof window !== 'undefined' ? window.location.origin : ''}/favicon.ico`;
    const notification = new Notification(title, {
      body,
      tag: String(n.id),
      icon,
      silent: false,
      data: { link: n.link ?? null },
    });
    notification.onclick = () => {
      void (async () => {
        await focusAppWindow();
        notification.close();
        await navigateToLink(n.link);
      })();
    };
  } catch (e) {
    console.warn('Notification failed', e);
  }
}
