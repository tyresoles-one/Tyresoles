/**
 * System configuration for the front-end application.
 * Runtime backend URL comes from app-config (see $lib/config/runtime.ts).
 */

import { get } from "svelte/store";
import { appConfigStore } from "$lib/config/runtime";
import { DEFAULT_APP_CONFIG } from "$lib/config/runtime";

export const MODE = import.meta.env.MODE;
export const isBrowser = typeof window !== "undefined";

/** Backend base URL from runtime config (sync; use after initAppConfig() in layout). */
export function getBackendBaseUrl(): string {
  const config = get(appConfigStore);
  return config?.backendBaseUrl ?? DEFAULT_APP_CONFIG.backendBaseUrl;
}

/** GraphQL HTTP endpoint. */
export function getGraphQLEndpoint(): string {
  return `${getBackendBaseUrl()}/graphql`;
}

/** GraphQL WebSocket endpoint. */
export function getGraphQLEndpointWs(): string {
  const base = getBackendBaseUrl();
  return base.replace(/^http:\/\//, "ws://").replace(/^https:\/\//, "wss://") + "/graphql";
}

/** For SSR/codegen: static fallback when store not yet populated. */
export const GRAPHQL_ENDPOINT_FALLBACK = `${DEFAULT_APP_CONFIG.backendBaseUrl}/graphql`;
export const GRAPHQL_ENDPOINT_WS_FALLBACK = GRAPHQL_ENDPOINT_FALLBACK.replace(
  "http://",
  "ws://",
).replace("https://", "wss://");

// Default error message
export const DEFAULT_ERROR_MESSAGE = "Oops! Something went wrong.";

/** Image entries with URLs derived from current backend (use after config ready). */
export function getImages(): { name: string; url: string }[] {
  const base = getBackendBaseUrl();
  return [
    { name: "man", url: `${base}/images/man.png` },
    { name: "man-1", url: `${base}/images/man_1.png` },
    { name: "man-2", url: `${base}/images/man_2.png` },
    { name: "man-3", url: `${base}/images/man_3.png` },
    { name: "man-4", url: `${base}/images/man_4.png` },
    { name: "woman", url: `${base}/images/woman.png` },
    { name: "woman-1", url: `${base}/images/woman_1.png` },
    { name: "woman-2", url: `${base}/images/woman_2.png` },
    { name: "login-cover", url: `${base}/images/login-cover.png` },
    { name: "birthday", url: `${base}/images/birthday.jpg` },
  ];
}

