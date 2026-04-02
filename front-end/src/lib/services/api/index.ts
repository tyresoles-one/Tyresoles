import { getBackendBaseUrl } from "$lib/config/system";
import { getAuthToken } from "$lib/stores/auth";
import { clearUserSession } from "$lib/services/auth/session";

/**
 * Enhanced fetch wrapper that automatically adds Auth headers and handles 401s globally.
 */
export async function secureFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const token = getAuthToken();
  const headers = new Headers(options.headers || {});

  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const baseUrl = getBackendBaseUrl();
  const finalUrl = url.startsWith("http") ? url : `${baseUrl}${url.startsWith("/") ? "" : "/"}${url}`;

  const response = await fetch(finalUrl, {
    ...options,
    headers
  });

  if (response.status === 401) {
    console.warn(`[API] 401 Unauthorized detected for ${finalUrl}. Triggering logout...`);
    clearUserSession();
  }

  return response;
}
