import { browser } from "$app/environment";
import { goto } from "$app/navigation";
import { Toast } from "$lib/components/venUI/toast";
import { clearAuth, getAuthToken } from "$lib/stores/auth";
import { NativeTokenStore } from "$lib/utils/native-token-store";

/**
 * Flag to prevent multiple concurrent 401 toasts/redirects
 */
let is401Handling = false;

export type SessionEndReason = "expired" | "revoked" | "unknown";

/**
 * Clear user session on authentication error and redirect to login.
 * Use `reason: 'revoked'` when the server invalidated the session (e.g. admin ended it).
 */
export async function clearUserSession(info?: { reason?: SessionEndReason }): Promise<void> {
  if (!browser) return;

  const reason = info?.reason ?? "expired";

  // Prevent multiple simultaneous 401 handling
  if (is401Handling) return;
  is401Handling = true;

  try {
    console.warn("[Auth] Clearing user session due to unauthorized response...", { reason });

    // Check if we were actually logged in (to avoid redundant toasts)
    const wasLoggedIn = !!getAuthToken() || !!localStorage.getItem('user');
    
    // Clear all auth state
    clearAuth();
    try {
      await NativeTokenStore.removeToken();
    } catch (e) {
      console.error("Failed to clear native token", e);
    }
    
    // Force clear localStorage completely for 'user' key
    try {
      localStorage.removeItem('user');
    } catch {}

    if (wasLoggedIn) {
      if (window.location.pathname !== "/login") {
        const title =
          reason === "revoked" ? "Session ended" : "Session expired";
        const description =
          reason === "revoked"
            ? "Your session is no longer active. Sign in again to continue."
            : "Your session has expired. Please log in again.";
        Toast.error(title, description);
        
        // Use a combination of goto and location.href for maximum reliability
        console.log("[Auth] Redirecting to login...");
        try {
          await goto("/login", { replaceState: true, invalidateAll: true });
        } catch (e) {
          window.location.href = "/login";
        }
      }
    }
  } catch (error) {
    console.error("Error clearing user session:", error);
  } finally {
    // Reset flag after a delay
    setTimeout(() => {
      is401Handling = false;
    }, 5000);
  }
}
