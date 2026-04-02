import { Preferences } from '@capacitor/preferences';

// Note: Tauri's plugin-store requires setup, but here is a simple LocalStorage fallback mock
// In a full Tauri setup you'd conditionally import 'tauri-plugin-store-api'

const TOKEN_KEY = 'tyresoles_auth_token';

/**
 * Checks if the current environment is running inside a Capacitor native app wrapper.
 */
export const isCapacitor = () => {
	return typeof window !== 'undefined' && 'Capacitor' in window && (window as any).Capacitor.isNative;
};

/**
 * Checks if the current environment is Tauri desktop/mobile.
 */
export const isTauri = () => {
	return typeof window !== 'undefined' && '__TAURI__' in window;
};

/**
 * A Universal Adapter for token storage.
 * Native plugins are asynchronous by default, so we use Promises.
 */
export const NativeTokenStore = {
	async setToken(token: string): Promise<void> {
		if (isCapacitor()) {
			await Preferences.set({ key: TOKEN_KEY, value: token });
		} else if (isTauri()) {
			// Tauri specific. If using plugin-store:
			// await store.set(TOKEN_KEY, token); await store.save();
			// Using localStorage for Tauri fallback unless explicitly installed.
			localStorage.setItem(TOKEN_KEY, token);
		} else {
			localStorage.setItem(TOKEN_KEY, token);
		}
	},

	async getToken(): Promise<string | null> {
		if (isCapacitor()) {
			const { value } = await Preferences.get({ key: TOKEN_KEY });
			return value;
		} else if (isTauri()) {
			return localStorage.getItem(TOKEN_KEY);
		} else {
			return localStorage.getItem(TOKEN_KEY);
		}
	},

	async removeToken(): Promise<void> {
		if (isCapacitor()) {
			await Preferences.remove({ key: TOKEN_KEY });
		} else if (isTauri()) {
			localStorage.removeItem(TOKEN_KEY);
		} else {
			localStorage.removeItem(TOKEN_KEY);
		}
	}
};
