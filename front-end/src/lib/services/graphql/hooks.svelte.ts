import { onMount } from 'svelte';
import { graphqlQuery, type GraphQLQueryOptions, type GraphQLResult } from './client';
import type { RequestDocument, Variables } from 'graphql-request';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

/**
 * Options for useGraphQLQuery hook
 */
export interface UseGraphQLQueryOptions<TVariables = Variables | Record<string, unknown>> extends GraphQLQueryOptions<TVariables> {
	enabled?: boolean;
	refetchOnMount?: boolean;
	onSuccess?: (data: unknown) => void;
	onError?: (error: string) => void;
}

/**
 * Result of useGraphQLQuery hook using Svelte 5 runes
 */
export interface UseGraphQLQueryResult<TData = unknown> {
	data: TData | undefined;
	loading: boolean;
	error: string | undefined;
	refetch: () => Promise<void>;
}

/**
 * Svelte 5 runes-based hook for GraphQL queries with reactive state
 * 
 * @example
 * ```svelte
 * <script>
 *   import { useGraphQLQuery } from '$lib/services/graphql/hooks';
 *   import { buildQuery } from '$lib/services/graphql/builder';
 *   
 *   const query = buildQuery`
 *     query GetUsers {
 *       users {
 *         id
 *         name
 *       }
 *     }
 *   `;
 *   
 *   const { data, loading, error, refetch } = useGraphQLQuery(query, {
 *     cacheKey: 'users',
 *     onSuccess: (data) => console.log('Users loaded:', data)
 *   });
 * </script>
 * 
 * {#if loading}
 *   <p>Loading...</p>
 * {:else if error}
 *   <p>Error: {error}</p>
 * {:else if data}
 *   <ul>
 *     {#each data.users as user}
 *       <li>{user.name}</li>
 *     {/each}
 *   </ul>
 * {/if}
 * ```
 */
export function useGraphQLQuery<TData = unknown, TVariables extends Variables = Variables>(
	document: RequestDocument,
	options: UseGraphQLQueryOptions<TVariables> = {}
): UseGraphQLQueryResult<TData> {
	const {
		enabled = true,
		refetchOnMount = true,
		onSuccess,
		onError,
		...queryOptions
	} = options;

	// Use Svelte 5 runes for reactive state
	let data = $state<TData | undefined>(undefined);
	let loading = $state<boolean>(false);
	let error = $state<string | undefined>(undefined);

	const executeQuery = async () => {
		if (!enabled) return;

		loading = true;
		error = undefined;

		const result = await graphqlQuery<TData, TVariables>(document, {
			...queryOptions,
			skipLoading: true // We manage loading state ourselves
		});

		loading = false;

		if (result.success && result.data) {
			data = result.data;
			onSuccess?.(result.data);
		} else {
			error = result.error;
			onError?.(result.error || 'An error occurred');
		}
	};

	const refetch = async () => {
		await executeQuery();
	};

	if (refetchOnMount) {
		onMount(() => {
			executeQuery();
		});
	}

	// Return reactive runes directly - Svelte 5 tracks $state automatically
	// No need for getters, runes are already reactive
	return {
		get data() {
			return data;
		},
		get loading() {
			return loading;
		},
		get error() {
			return error;
		},
		refetch
	};
}

/**
 * Options for useGraphQLMutation hook
 */
export interface UseGraphQLMutationOptions<TVariables = Variables> {
	onSuccess?: (data: unknown) => void;
	onError?: (error: string) => void;
	skipLoading?: boolean;
}

/**
 * Result of useGraphQLMutation hook using Svelte 5 runes
 */
export interface UseGraphQLMutationResult<TData = unknown, TVariables = Variables> {
	mutate: (variables?: TVariables) => Promise<GraphQLResult<TData>>;
	loading: boolean;
	error: string | undefined;
}

/**
 * Svelte 5 runes-based hook for GraphQL mutations with reactive state
 * Supports both TypedDocumentNode (from codegen) and RequestDocument (legacy)
 * 
 * @example
 * ```svelte
 * <script>
 *   import { useGraphQLMutation } from '$lib/services/graphql/hooks';
 *   import { CreateUserDocument } from '$lib/services/graphql/generated';
 *   
 *   const { mutate, loading, error } = useGraphQLMutation(CreateUserDocument, {
 *     onSuccess: (data) => console.log('User created:', data)
 *   });
 *   
 *   async function handleSubmit() {
 *     await mutate({ input: { name: 'John', email: 'john@example.com' } });
 *   }
 * </script>
 * ```
 */
export function useGraphQLMutation<
	TData = unknown,
	TVariables extends Variables = Variables | Record<string, unknown>
>(
	document: TypedDocumentNode<TData, TVariables> | RequestDocument,
	options: UseGraphQLMutationOptions<TVariables> = {}
): UseGraphQLMutationResult<TData, TVariables> {
	const { onSuccess, onError, skipLoading = false } = options;

	// Use Svelte 5 runes for reactive state
	let loading = $state<boolean>(false);
	let error = $state<string | undefined>(undefined);

	const mutate = async (variables?: TVariables): Promise<GraphQLResult<TData>> => {
		loading = true;
		error = undefined;

		const { graphqlMutation } = await import('./client');
		const result = await graphqlMutation<TData, TVariables>(document, {
			variables,
			skipLoading: true // We manage loading state ourselves
		});

		loading = false;

		if (result.success && result.data) {
			onSuccess?.(result.data);
		} else {
			error = result.error;
			onError?.(result.error || 'An error occurred');
		}

		return result;
	};

	// Return reactive object - Svelte 5 will track $state variables
	return {
		mutate,
		get loading() {
			return loading;
		},
		get error() {
			return error;
		}
	};
}
