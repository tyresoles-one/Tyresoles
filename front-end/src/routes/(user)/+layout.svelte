<script lang="ts">
	/**
	 * User layout: wraps protected routes with RouteGuard.
	 * - Checks login (redirects to /login if not authenticated)
	 * - Checks permission based on current path (shows NoPermission if not permitted)
	 */
	import { page } from '$app/stores';
	import { RouteGuard } from '$lib/components/venUI/routeGuard';
	import { getRequiredPermissionForPath } from '$lib/components/venUI/routeGuard/routePermissions';

	let { children } = $props();

	const requiredPermission = $derived(getRequiredPermissionForPath($page.url.pathname));
</script>

<RouteGuard {requiredPermission}>
	{@render children()}
</RouteGuard>
