<script lang="ts">
	/**
	 * Route guard: enforces login and permission checks.
	 * - If not logged in: redirects to login page
	 * - If logged in with userType "super": allows access (bypasses permission check)
	 * - If logged in but required permission not in login menus: shows NoPermission
	 * - If permitted: renders slot content
	 *
	 * Permissions are derived from authStore.menus (login response): each menu item's action and label.
	 * Paths are mapped to permission names via ROUTE_PERMISSIONS.
	 *
	 * @example
	 * ```svelte
	 * <RouteGuard requiredPermission="Dealers">
	 *   <DealersPage />
	 * </RouteGuard>
	 * ```
	 *
	 * @example With custom login path
	 * ```svelte
	 * <RouteGuard requiredPermission="Users" loginPath="/auth/login">
	 *   <UsersPage />
	 * </RouteGuard>
	 * ```
	 */
	import { goto } from '$app/navigation';
	import { page } from '$app/stores';
	import { authStore } from '$lib/stores/auth';
	import { browser } from '$app/environment';
	import { ROUTE_PERMISSIONS } from './routePermissions';
	import NoPermission from './NoPermission.svelte';

	type Props = {
		/** Permission required to view content. Must match permission.name, permission.roleId, or be in permission.values. Omit to only check login. */
		requiredPermission?: string;
		/** Path to redirect when not logged in (default: /login) */
		loginPath?: string;
		/** Additional CSS classes for the wrapper */
		class?: string;
		/** Slot content shown when permitted */
		children: import('svelte').Snippet;
	};

	let {
		requiredPermission = '',
		loginPath = '/login',
		class: className = '',
		children,
	}: Props = $props();

	type GuardState = 'checking' | 'unauthenticated' | 'forbidden' | 'redirectChangePassword' | 'allowed';

	let state = $state<GuardState>('checking');

	/** Collect allowed permission names from auth.menus (item action/label; path → name via ROUTE_PERMISSIONS). */
	function getAllowedPermissionNames(): Set<string> {
		const menus = authStore.get().menus ?? [];
		const names = new Set<string>();
		for (const menu of menus) {
			for (const sub of menu.subMenus ?? []) {
				for (const item of sub.items ?? []) {
					const action = (item.action ?? '').trim();
					const label = (item.label ?? '').trim();
					if (action) {
						const permName = ROUTE_PERMISSIONS[action] ?? action;
						names.add(permName.toLowerCase());
					}
					if (label) names.add(label.toLowerCase());
				}
			}
		}
		return names;
	}

	function hasPermission(allowed: Set<string>, required: string): boolean {
		const r = (required || '').trim().toLowerCase();
		if (!r) return false;
		if (allowed.has(r)) return true;
		for (const a of allowed) {
			if (a.includes(',') && a.split(',').some((v) => v.trim().toLowerCase() === r)) return true;
		}
		return false;
	}

	function checkAccess(): void {
		const auth = authStore.get();
		const pathname = $page.url.pathname;

		if (!auth.token) {
			if (pathname === '/change-password') {
				state = 'allowed';
				return;
			}
			state = 'unauthenticated';
			return;
		}

		if (auth.requirePasswordChange && pathname !== '/change-password') {
			state = 'redirectChangePassword';
			return;
		}

		if (auth.user?.userType?.toLowerCase() === 'super') {
			state = 'allowed';
			return;
		}

		const required = requiredPermission.trim();
		if (!required) {
			state = 'allowed';
			return;
		}

		const allowed = getAllowedPermissionNames();
		state = hasPermission(allowed, required) ? 'allowed' : 'forbidden';
	}

	// Re-check on auth or pathname change so requirePasswordChange blocks all routes except /change-password (client only)
	$effect(() => {
		if (!browser) return;
		const _path = $page.url.pathname;
		const unsub = authStore.subscribe(() => checkAccess());
		checkAccess();
		return () => unsub();
	});

	// Redirect when unauthenticated (client only)
	$effect(() => {
		if (state === 'unauthenticated' && browser) {
			goto(loginPath, { replaceState: true });
		}
	});

	// Redirect when password change required (client only)
	$effect(() => {
		if (state === 'redirectChangePassword' && browser) {
			goto('/change-password', { replaceState: true });
		}
	});
</script>

{#if state === 'checking'}
	<div class="flex items-center justify-center min-h-[200px] {className}">
		<div class="animate-pulse text-muted-foreground">Checking access...</div>
	</div>
{:else if state === 'redirectChangePassword'}
	<div class="flex items-center justify-center min-h-[200px] {className}">
		<div class="animate-pulse text-muted-foreground">Redirecting to change password...</div>
	</div>
{:else if state === 'forbidden'}
	<NoPermission {requiredPermission} class={className} />
{:else if state === 'allowed'}
	{@render children()}
{/if}
