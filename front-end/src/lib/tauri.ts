/**
 * Tauri desktop environment helpers.
 * Use these to branch behavior when running inside the Tauri app (e.g. window controls, system tray).
 * In the browser, isTauri() is false and Tauri APIs are not available.
 *
 * Detection: We check both __TAURI__ and __TAURI_INTERNALS__ because in production
 * tauri.conf.json has withGlobalTauri: false by default, so only __TAURI_INTERNALS__
 * is injected. If we only checked __TAURI__, the app would take the "web" config path
 * and fetch /app-config.json from the bundled static assets instead of calling the
 * Rust read_app_config (which reads from the installed folder).
 */

declare global {
	interface Window {
		__TAURI__?: { core?: { invoke?: (cmd: string, args?: unknown) => Promise<unknown> } };
		__TAURI_INTERNALS__?: { invoke?: (cmd: string, args?: unknown, options?: unknown) => Promise<unknown> };
	}
}

/** True when the app is running inside the Tauri desktop shell. */
export function isTauri(): boolean {
	if (typeof window === 'undefined') return false;
	// withGlobalTauri is false by default: only __TAURI_INTERNALS__ is set in production
	return (
		typeof (window as Window).__TAURI_INTERNALS__ !== 'undefined' ||
		typeof (window as Window).__TAURI__ !== 'undefined'
	);
}

/**
 * Gets the Tauri invoke function.
 * Supports both withGlobalTauri: true (__TAURI__.core.invoke)
 * and the default v2 internal injection (__TAURI_INTERNALS__.invoke).
 */
export function getInvoke() {
	if (typeof window === 'undefined') return null;

	const win = window as Window;

	// v2 default/production (withGlobalTauri: false)
	if (win.__TAURI_INTERNALS__?.invoke) {
		return win.__TAURI_INTERNALS__.invoke;
	}

	// v2 withGlobalTauri: true
	if (win.__TAURI__?.core?.invoke) {
		return win.__TAURI__?.core?.invoke;
	}

	return null;
}
