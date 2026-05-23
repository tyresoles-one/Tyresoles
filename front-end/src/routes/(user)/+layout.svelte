<script lang="ts">
	/**
	 * User layout: wraps protected routes with RouteGuard.
	 * - Checks login (redirects to /login if not authenticated)
	 * - Checks permission based on current path (shows NoPermission if not permitted)
	 */
	import { page } from '$app/stores';
	import { RouteGuard } from '$lib/components/venUI/routeGuard';
	import { getRequiredPermissionForPath } from '$lib/components/venUI/routeGuard/routePermissions';

	import { onMount, onDestroy } from 'svelte';
	import { isTauri } from '$lib/tauri';

	let { children } = $props();
	let teardownDriveSyncWatcher: null | (() => void) = null;

	const requiredPermission = $derived(getRequiredPermissionForPath($page.url.pathname));

	onMount(async () => {
		if (!isTauri()) return;
		const { initDriveSyncWatcher, stopDriveSyncWatcher } = await import('$lib/services/driveSyncWatcher');
		initDriveSyncWatcher();
		teardownDriveSyncWatcher = () => stopDriveSyncWatcher();
	});

	onDestroy(() => {
		teardownDriveSyncWatcher?.();
	});
</script>

<RouteGuard {requiredPermission}>
	{@render children()}
</RouteGuard>
