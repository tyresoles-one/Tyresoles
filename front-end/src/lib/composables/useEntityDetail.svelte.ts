/**
 * Composable for loading single entity details
 * 
 * @example
 * ```typescript
 * const dealer = useEntityDetail<GetDealerByCodeQuery, Dealer>({
 *   id: code,
 *   query: GetDealerByCodeDocument,
 *   dataPath: 'dealerByCode',
 *   cacheKey: (id) => `dealer-${id}`,
 *   idParamName: 'code'
 * });
 * 
 * // Access data
 * {#if dealer.loading}
 *   <Skeleton />
 * {:else if dealer.error}
 *   <Error message={dealer.error} />
 * {:else if dealer.entity.value}
 *   <div>{dealer.entity.value.name}</div>
 * {/if}
 * ```
 */

import { graphqlQuery } from '$lib/services/graphql';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export interface UseEntityDetailConfig<TQuery> {
	/** Entity ID (code, userName, etc.) or getter for reactive updates */
	id: string | (() => string);
	/** GraphQL query document */
	query: TypedDocumentNode<TQuery, any>;
	/** Path to entity in query response (e.g., 'dealerByCode') */
	dataPath: string;
	/** Function to generate cache key */
	cacheKey: (id: string) => string;
	/** Name of ID parameter in GraphQL query (default: auto-detect from dataPath) */
	idParamName?: string;
	/** Cache TTL in milliseconds (default: 30000) */
	cacheTTL?: number;
}

export interface UseEntityDetailReturn<TEntity> {
	/** Entity data with getter/setter */
	entity: {
		get value(): TEntity | null;
		set value(v: TEntity | null);
	};
	/** Loading state */
	loading: boolean;
	/** Error message if any */
	error: string | undefined;
	/** Reload entity data */
	reload: () => Promise<void>;
}

/**
 * Auto-detect ID parameter name from dataPath
 * e.g., 'dealerByCode' -> 'code', 'userByName' -> 'name'
 */
function detectIdParam(dataPath: string): string {
	if (dataPath.includes('ByCode')) return 'code';
	if (dataPath.includes('ById')) return 'id';
	if (dataPath.includes('ByName')) return 'name';
	if (dataPath.includes('ByUserName')) return 'userName';
	return 'id'; // fallback
}

/**
 * Load and manage single entity detail
 */
export function useEntityDetail<TQuery, TEntity>(
	config: UseEntityDetailConfig<TQuery>
): UseEntityDetailReturn<TEntity> {
	let loading = $state(true);
	let error = $state<string | undefined>(undefined);
	let entity = $state<TEntity | null>(null);

	const idParam = config.idParamName ?? detectIdParam(config.dataPath);

	function getCurrentId(): string {
		return typeof config.id === 'function' ? config.id() : config.id;
	}

	async function load() {
		const currentId = getCurrentId();
		if (!currentId) {
			loading = false;
			return;
		}

		loading = true;
		error = undefined;
		entity = null;

		try {
			const result = await graphqlQuery<TQuery>(config.query, {
				variables: { [idParam]: currentId },
				cacheKey: config.cacheKey(currentId),
				cacheTTL: config.cacheTTL ?? 30_000,
				silent: true
			});

			if (result.success && result.data) {
				entity = ((result.data as any)[config.dataPath] ?? null) as TEntity;
			} else {
				error = result.error ?? 'Failed to load';
			}
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load';
		} finally {
			loading = false;
		}
	}

	// Auto-load when ID changes (getter ensures reactive re-run)
	$effect(() => {
		if (getCurrentId()) load();
	});

	return {
		entity: {
			get value() {
				return entity;
			},
			set value(v: TEntity | null) {
				entity = v;
			}
		},
		get loading() {
			return loading;
		},
		get error() {
			return error;
		},
		reload: load
	};
}
