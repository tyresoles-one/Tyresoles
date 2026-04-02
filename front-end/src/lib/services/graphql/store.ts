/**
 * Global GraphQL loading state
 * This provides a reactive store that can be used across components
 * Compatible with SSR and optimized for performance
 * 
 * Note: For Svelte 5 runes, use this in components with $state rune:
 * ```svelte
 * <script>
 *   import { graphqlLoading } from '$lib/services/graphql/store';
 *   let isLoading = $state($graphqlLoading);
 * </script>
 * ```
 * 
 * Or use the store directly:
 * ```svelte
 * {#if $graphqlLoading}
 *   <div>Loading...</div>
 * {/if}
 * ```
 */

import { isLoading, subscribeLoading } from './client';
import { writable } from 'svelte/store';

/**
 * Reactive loading state store using Svelte stores (SSR compatible)
 * Optimized with efficient subscription mechanism
 * 
 * @example
 * ```svelte
 * <script>
 *   import { graphqlLoading } from '$lib/services/graphql/store';
 * </script>
 * 
 * {#if $graphqlLoading}
 *   <div>Loading...</div>
 * {/if}
 * ```
 */
const createLoadingStore = () => {
	const { subscribe, set } = writable(false);
	let rafId: number | null = null;
	let lastValue = false;
	let isSubscribed = false;

	const poll = () => {
		if (typeof window === 'undefined' || !isSubscribed) return;
		
		const currentValue = isLoading();
		if (currentValue !== lastValue) {
			lastValue = currentValue;
			set(currentValue);
		}
		rafId = requestAnimationFrame(poll);
	};

	const startPolling = () => {
		if (typeof window === 'undefined' || isSubscribed) return;
		isSubscribed = true;
		lastValue = isLoading();
		set(lastValue);
		poll();
	};

	const stopPolling = () => {
		if (rafId !== null) {
			cancelAnimationFrame(rafId);
			rafId = null;
		}
		isSubscribed = false;
	};

	// Start polling only on client when first subscriber appears
	let subscriberCount = 0;
	return {
		subscribe: (callback: (value: boolean) => void) => {
			subscriberCount++;
			if (subscriberCount === 1) {
				startPolling();
			}
			
			const unsubscribe = subscribe(callback);
			
			return () => {
				unsubscribe();
				subscriberCount--;
				if (subscriberCount === 0) {
					stopPolling();
				}
			};
		}
	};
};

/**
 * Global GraphQL loading state store
 * Subscribe to this in components to reactively track loading state
 * SSR compatible - only polls on client side when subscribed
 * Optimized - stops polling when no subscribers
 */
export const graphqlLoading = createLoadingStore();
