/**
 * serviceChecker.ts
 * ------------------
 * Typed wrapper around the Tauri `check_services`, `start_service`,
 * `stop_service`, and `restart_service` commands.
 *
 * All functions are no-ops (returning a "not available" status) when the app
 * is running in a regular browser so callers don't need to guard every call.
 */

import { getInvoke, isTauri } from '$lib/tauri';

// ── Types ──────────────────────────────────────────────────────────────────

export type ServiceState =
	| 'Running'
	| 'Stopped'
	| 'StartPending'
	| 'StopPending'
	| 'PausePending'
	| 'Paused'
	| 'ContinuePending'
	| 'Unknown';

export interface ServiceStatus {
	/** Windows short service name */
	name: string;
	/** Human-readable display name from the SCM */
	displayName: string;
	/** Current state string */
	state: ServiceState;
	/** Convenience boolean */
	isRunning: boolean;
	/** Whether the UI should show a "Start" button */
	canStart: boolean;
	/** Whether the UI should show a "Stop" button */
	canStop: boolean;
}

export interface ServiceDescriptor {
	name: string;
	canStart?: boolean;
	canStop?: boolean;
}

// ── Helpers ────────────────────────────────────────────────────────────────

function unavailable(name: string): ServiceStatus {
	return {
		name,
		displayName: name,
		state: 'Unknown',
		isRunning: false,
		canStart: false,
		canStop: false
	};
}

async function invokeCmd<T>(cmd: string, args?: Record<string, unknown>): Promise<T> {
	if (!isTauri()) throw new Error('Service management is only available in the desktop app.');
	const invoke = getInvoke();
	if (!invoke) throw new Error('Tauri invoke not available.');
	return invoke(cmd, args) as Promise<T>;
}

// ── Public API ─────────────────────────────────────────────────────────────

/**
 * Check the status of one or more services in a single call.
 * Returns an array of ServiceStatus in the same order as the input descriptors.
 *
 * @example
 * const statuses = await checkServices([
 *   { name: 'TyrsolesApi', canStart: true, canStop: true },
 *   { name: 'MSSQLSERVER', canStart: false, canStop: false },
 * ]);
 */
export async function checkServices(services: ServiceDescriptor[]): Promise<ServiceStatus[]> {
	if (!isTauri()) return services.map((s) => unavailable(s.name));
	try {
		return await invokeCmd<ServiceStatus[]>('check_services', {
			input: { services }
		});
	} catch (e) {
		console.error('[serviceChecker] check_services failed:', e);
		return services.map((s) => unavailable(s.name));
	}
}

/**
 * Start a single Windows service. Polls until Running or timeout (~5 s).
 * Returns the final ServiceStatus.
 */
export async function startService(serviceName: string): Promise<ServiceStatus> {
	if (!isTauri()) return unavailable(serviceName);
	return invokeCmd<ServiceStatus>('start_service', { serviceName });
}

/**
 * Stop a single Windows service. Polls until Stopped or timeout (~5 s).
 * Returns the final ServiceStatus.
 */
export async function stopService(serviceName: string): Promise<ServiceStatus> {
	if (!isTauri()) return unavailable(serviceName);
	return invokeCmd<ServiceStatus>('stop_service', { serviceName });
}

/**
 * Restart a single Windows service (stop → start).
 * Returns the final ServiceStatus after restart.
 */
export async function restartService(serviceName: string): Promise<ServiceStatus> {
	if (!isTauri()) return unavailable(serviceName);
	return invokeCmd<ServiceStatus>('restart_service', { serviceName });
}
