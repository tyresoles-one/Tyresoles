import { browser } from "$app/environment";
import { getAuthToken, authStore } from "$lib/stores/auth";
import { clearUserSession } from "./session";

// 15 minutes of inactivity
const IDLE_TIMEOUT_MS = 15 * 60 * 1000;
// Warning before timeout
const WARNING_BEFORE_MS = 5 * 60 * 1000;

let idleTimer: ReturnType<typeof setTimeout> | null = null;
let lastActivity = Date.now();

function handleActivity() {
  lastActivity = Date.now();
  resetTimer();
}

function checkIdle() {
  const now = Date.now();
  const diff = now - lastActivity;
  
  const authData = authStore.get();
  if (!authData.token) return;

  if (diff >= IDLE_TIMEOUT_MS) {
    clearUserSession({ reason: "expired" });
    return;
  }
  
  if (authData.expiresAt) {
    const expiresAtMs = new Date(authData.expiresAt).getTime();
    if (expiresAtMs - now < WARNING_BEFORE_MS) {
      if (expiresAtMs - now <= 0) {
        clearUserSession({ reason: "expired" });
        return;
      } else {
        // Warning logic if needed, but for now we just handle expiration
      }
    }
  }

  // schedule next check
  idleTimer = setTimeout(checkIdle, Math.min(IDLE_TIMEOUT_MS - diff, 60000));
}

function resetTimer() {
  if (idleTimer) {
    clearTimeout(idleTimer);
  }
  
  if (!getAuthToken()) {
    return; // Don't run timer if not logged in
  }

  idleTimer = setTimeout(checkIdle, IDLE_TIMEOUT_MS);
}

export function initIdleTimer() {
  if (!browser) return;
  
  ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart'].forEach(event => {
    window.addEventListener(event, handleActivity, { passive: true });
  });

  resetTimer();
}

export function cleanupIdleTimer() {
  if (!browser) return;

  ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart'].forEach(event => {
    window.removeEventListener(event, handleActivity);
  });
  
  if (idleTimer) {
    clearTimeout(idleTimer);
    idleTimer = null;
  }
}
