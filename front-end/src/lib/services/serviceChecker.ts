/**
 * serviceChecker.ts
 * ------------------
 * Typed wrapper around the backend /api/windows-services REST endpoints.
 *
 * Admins in the browser can query, start, stop, and restart allowlisted
 * Windows services on the IIS host.
 */

import { secureFetch } from '$lib/services/api';
import type { ServiceDescriptor, ServiceState, ServiceStatus } from './serviceChecker.types';

export type { ServiceDescriptor, ServiceState, ServiceStatus } from './serviceChecker.types';

async function parseResponse<T>(response: Response): Promise<T> {
	if (response.ok) {
		return response.json() as Promise<T>;
	}

	let message = `Request failed (${response.status})`;
	try {
		const body = (await response.json()) as { error?: string };
		if (body.error) message = body.error;
	} catch {
		/* ignore */
	}
	throw new Error(message);
}

/**
 * Fetch status of all configured Windows services from the backend allowlist.
 */
export async function checkServices(_services?: ServiceDescriptor[]): Promise<ServiceStatus[]> {
	const response = await secureFetch('/api/windows-services');
	return parseResponse<ServiceStatus[]>(response);
}

/**
 * Start a single Windows service. Polls on the server until Running or timeout.
 */
export async function startService(serviceName: string): Promise<ServiceStatus> {
	const response = await secureFetch(
		`/api/windows-services/${encodeURIComponent(serviceName)}/start`,
		{ method: 'POST' }
	);
	return parseResponse<ServiceStatus>(response);
}

/**
 * Stop a single Windows service. Polls on the server until Stopped or timeout.
 */
export async function stopService(serviceName: string): Promise<ServiceStatus> {
	const response = await secureFetch(
		`/api/windows-services/${encodeURIComponent(serviceName)}/stop`,
		{ method: 'POST' }
	);
	return parseResponse<ServiceStatus>(response);
}

/**
 * Restart a single Windows service (stop then start).
 */
export async function restartService(serviceName: string): Promise<ServiceStatus> {
	const response = await secureFetch(
		`/api/windows-services/${encodeURIComponent(serviceName)}/restart`,
		{ method: 'POST' }
	);
	return parseResponse<ServiceStatus>(response);
}
