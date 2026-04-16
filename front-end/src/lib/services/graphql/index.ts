/**
 * GraphQL Network Service
 *
 * A performant and developer-friendly GraphQL client for the front-end application.
 *
 * Features:
 * - Type-safe queries and mutations
 * - Automatic authentication token handling
 * - Request caching for improved performance
 * - Error handling with user-friendly messages
 * - Loading state management
 * - Svelte hooks for reactive queries and mutations
 *
 * @example Basic Query
 * ```ts
 * import { graphqlQuery, buildQuery } from '$lib/services/graphql';
 *
 * const query = buildQuery`
 *   query GetUsers {
 *     users {
 *       id
 *       name
 *       email
 *     }
 *   }
 * `;
 *
 * const result = await graphqlQuery(query, {
 *   cacheKey: 'users',
 *   cacheTTL: 60000 // 1 minute
 * });
 *
 * if (result.success) {
 *   console.log(result.data);
 * }
 * ```
 *
 * @example Basic Mutation
 * ```ts
 * import { graphqlMutation, buildMutation } from '$lib/services/graphql';
 *
 * const mutation = buildMutation`
 *   mutation CreateUser($input: UserInput!) {
 *     createUser(input: $input) {
 *       id
 *       name
 *     }
 *   }
 * `;
 *
 * const result = await graphqlMutation(mutation, {
 *   variables: {
 *     input: {
 *       name: 'John Doe',
 *       email: 'john@example.com'
 *     }
 *   }
 * });
 * ```
 *
 * @example Using Svelte Hooks
 * ```svelte
 * <script>
 *   import { useGraphQLQuery, buildQuery } from '$lib/services/graphql';
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
 *     cacheKey: 'users'
 *   });
 * </script>
 *
 * {#if $loading}
 *   <p>Loading...</p>
 * {:else if $error}
 *   <p>Error: {$error}</p>
 * {:else if $data}
 *   <ul>
 *     {#each $data.users as user}
 *       <li>{user.name}</li>
 *     {/each}
 *   </ul>
 * {/if}
 * ```
 */

// Core client functions
export {
  graphqlQuery,
  graphqlMutation,
  clearGraphQLCache,
  getGraphQLClient,
  GraphQLNetworkError,
  isLoading,
  subscribeLoading,
} from "./client";

// Global config and error handling
export {
  setGraphQLConfig,
  getGraphQLConfig,
  notifyError,
  handleGraphQLError,
  extractBestGraphQLErrorMessage,
} from "./config";

export type { GraphQLConfig, GraphQLErrorContext, UnauthorizedInfo } from "./config";

// Type definitions
export type {
  GraphQLQueryOptions,
  GraphQLMutationOptions,
  GraphQLResult,
} from "./client";

export type {
  GraphQLError,
  GraphQLResponse,
  GraphQLRequestOptions,
} from "./types";

// Builder utilities
export {
  buildQuery,
  buildMutation,
  buildFragment,
  createCacheKey,
} from "./builder";

// Svelte hooks (for reactive queries/mutations)
export { useGraphQLQuery, useGraphQLMutation } from "./hooks.svelte";

// Global loading state store
export { graphqlLoading } from "./store";

export { createListQueryConfig, usePaginatedQuery } from "./listQuery.svelte";

export type {
  PaginatedListResult,
  ListQueryConfig,
  UsePaginatedQueryResult,
} from "./listQuery.svelte";

export type {
  UseGraphQLQueryOptions,
  UseGraphQLQueryResult,
  UseGraphQLMutationOptions,
  UseGraphQLMutationResult,
} from "./hooks.svelte";
