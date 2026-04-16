/**
 * Authentication store: persisted user session data.
 * Matches the login response from the backend (LoginResult + stored fields).
 * Syncs to localStorage with key "user".
 */

import { createPersistedStore } from './persisted.js';
import type { LoginUser, Menu, UserLocation } from '$lib/services/graphql/generated/types.js';

export type AuthData = {
	token: string;
	refreshToken: string;
	username: string;
	userSpecialToken: string;
	expiresAt: string;
	user: LoginUser | null;
	/** Navigation menus from login response (Menu[]). */
	menus: Menu[] | null;
	/** Locations from login response (Location[]). */
	locations: UserLocation[] | null;
	/** When true, user must complete change-password. Use requirePasswordChangeReason to choose form (Security PIN vs current password). */
	requirePasswordChange?: boolean;
	/** FirstLogin = Security PIN form; AdminReset or Expired = current password form. */
	requirePasswordChangeReason?: string | null;
};

const initialAuthData: AuthData = {
	token: '',
	refreshToken: '',
	username: '',
	userSpecialToken: '',
	expiresAt: '',
	user: null,
	menus: null,
	locations: null,
	requirePasswordChange: false,
	requirePasswordChangeReason: null,
};

/**
 * Global auth store: persisted to localStorage.
 * Use this to access/update the current user session.
 *
 * @example
 * ```svelte
 * <script>
 *   import { authStore } from '$lib/stores/auth';
 * </script>
 *
 * {#if $authStore.token}
 *   <p>Welcome, {$authStore.username}!</p>
 * {/if}
 * ```
 *
 * @example Set auth data after login
 * ```ts
 * authStore.set({
 *   token: 'abc123',
 *   username: 'user@example.com',
 *   expiresAt: '2026-02-01T12:00:00Z',
 *   user: { ... }
 * });
 * ```
 *
 * @example Clear auth data on logout
 * ```ts
 * authStore.clear();
 * ```
 */
export const authStore = createPersistedStore('user', initialAuthData, {
	debounceMs: 0, // Write immediately for auth (critical data)
});

/**
 * Check if user is authenticated (has a valid token).
 */
export function isAuthenticated(): boolean {
	const { token } = authStore.get();
	return !!token;
}

/**
 * Get current auth token (for API calls).
 */
export function getAuthToken(): string {
	return authStore.get().token;
}

export function getRefreshToken(): string {
	return authStore.get().refreshToken;
}

export function getUser(): LoginUser | null {
	return authStore.get().user;
}
/**
 * Clear auth data (logout).
 */
export function clearAuth(): void {
	authStore.set(initialAuthData);
}
