/**
 * OS-level notifications for in-app GraphQL notifications.
 * - Browser: Web Notifications API (HTTPS / localhost; iOS has limited support).
 * - Tauri: @tauri-apps/plugin-notification after permission.
 */

import { isTauri } from '$lib/tauri';

const STORAGE_KEY = 'tyresoles_nativeAlertsEnabled';

/** When true, only show native toast if the document tab is in the background (avoids duplicate with in-app bell). */
const ONLY_WHEN_TAB_NOT_VISIBLE = true;

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
export function getWebNotificationPermission(): NotificationPermission | 'unsupported' {
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
  const p = getWebNotificationPermission();
  return p === 'unsupported' ? 'Not supported in this browser' : p;
}

/**
 * Request OS permission for notifications (must run from a user gesture in browsers).
 */
export async function requestNativeNotificationPermission(): Promise<
  'granted' | 'denied' | 'default' | 'unsupported'
> {
  if (isTauri()) {
    try {
      const { isPermissionGranted, requestPermission } = await import('@tauri-apps/plugin-notification');
      const granted = await isPermissionGranted();
      if (granted) return 'granted';
      const result = await requestPermission();
      return result === 'granted' ? 'granted' : 'denied';
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
  if (!isWebNotificationSupported()) return false;
  return Notification.permission === 'granted';
}

function shouldShowNativeToast(): boolean {
  if (!isNativeAlertsEnabled()) return false;
  if (typeof document === 'undefined') return true;
  if (ONLY_WHEN_TAB_NOT_VISIBLE && document.visibilityState === 'visible') return false;
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
  window.focus();
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

  /*
   * Browser and Tauri both use the Web Notifications API in the webview (the plugin’s
   * sendNotification is a thin wrapper around `new Notification`). Using one path gives
   * reliable click → focus + navigate on every platform.
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
