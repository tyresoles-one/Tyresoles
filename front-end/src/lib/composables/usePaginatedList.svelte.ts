/**
 * Composable for managing paginated list data with search
 * 
 * @example
 * ```typescript
 * const list = usePaginatedList<User>({
 *   query: GetUsersDocument,
 *   dataPath: 'users',
 *   pageSize: 50
 * });
 * 
 * // In template
 * <MasterList
 *   items={list.items}
 *   bind:searchQuery={list.searchQuery.value}
 *   loading={list.loading}
 *   onLoadMore={list.onLoadMore}
 *   onRefresh={list.onRefresh}
 * />
 * ```
 */

import { SmartPagination } from '$lib/utils/pagination/store.svelte';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export interface UsePaginatedListConfig<T> {
	/** GraphQL query document */
	query: TypedDocumentNode<any, any>;
	/** Path to data in query response (e.g., 'users', 'dealers') */
	dataPath: string;
	/** Items per page */
	pageSize?: number;
	/** Strategy for pagination */
	strategy?: 'server' | 'client';
	/** Skip cache for fresh data */
	skipCache?: boolean;
	/** Maps search text to GraphQL variables (e.g. `where`). See SmartPagination. */
	mapSearchToVariables?: (term: string) => Record<string, unknown>;
	/** See SmartPagination `serverVariableAllowlist`. */
	serverVariableAllowlist?: readonly string[];
	/** See SmartPagination `paginationMode` / `pageInfoPath`. */
	paginationMode?: 'offset' | 'cursor';
	pageInfoPath?: string;
}

export interface UsePaginatedListReturn<T> {
	/** Paginated items */
	items: T[];
	/** Loading state for initial load */
	loading: boolean;
	/** Loading state for loading more items */
	loadingMore: boolean;
	/** Error message if any */
	error: string | undefined;
	/** Whether more items are available */
	hasMore: boolean;
	/** Total count from API (for server pagination). Use in MasterList totalCount prop. */
	totalCount: number;
	/** Search query with getter/setter */
	searchQuery: {
		get value(): string;
		set value(v: string);
	};
	/** Load next page */
	onLoadMore: () => void;
	/** Refresh/reload data */
	onRefresh: () => void;
	/** Access to underlying pagination instance for filters */
	pagination: SmartPagination<T>;
}

/**
 * Creates a paginated list with search functionality
 */
export function usePaginatedList<T>(
	config: UsePaginatedListConfig<T>
): UsePaginatedListReturn<T> {
	let searchQuery = $state('');

	const pagination = new SmartPagination<T>({
		query: config.query,
		strategy: config.strategy ?? 'server',
		pageSize: config.pageSize ?? 50,
		dataPath: config.dataPath,
		skipCache: config.skipCache ?? true,
		mapSearchToVariables: config.mapSearchToVariables,
		serverVariableAllowlist: config.serverVariableAllowlist,
		paginationMode: config.paginationMode,
		pageInfoPath: config.pageInfoPath
	});

	/** Ensures Svelte tracks list updates (plain getters on `pagination.items` alone can miss invalidation). */
	const items = $derived(pagination.items);

	/**
	 * Sync search to pagination only when the user changes the term.
	 * Do not run debounced `setSearch` on mount: the constructor already loads, and a delayed
	 * `reset()` + `load()` would clear cursor state and break "Load More" (cursor pagination).
	 */
	let previousSearchTerm: string | null = null;
	$effect(() => {
		const q = searchQuery;
		if (previousSearchTerm === null) {
			previousSearchTerm = q;
			return;
		}
		if (q === previousSearchTerm) return;
		previousSearchTerm = q;
		pagination.setSearch(q);
	});

	return {
		get items() {
			return items;
		},
		get loading() {
			return pagination.loading;
		},
		get loadingMore() {
			return pagination.loadingMore;
		},
		get error() {
			return pagination.error;
		},
		get hasMore() {
			return pagination.hasMore;
		},
		get totalCount() {
			return pagination.totalCount;
		},
		searchQuery: {
			get value() {
				return searchQuery;
			},
			set value(v: string) {
				searchQuery = v;
			}
		},
		onLoadMore: () => pagination.nextPage(),
		onRefresh: () => {
			pagination.reset();
			pagination.load();
		},
		pagination
	};
}
