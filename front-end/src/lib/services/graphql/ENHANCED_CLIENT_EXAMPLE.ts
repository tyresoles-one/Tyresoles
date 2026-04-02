/**
 * Enhanced GraphQL Client Example
 * 
 * This file demonstrates how the client would look with code generation.
 * This is a reference implementation - actual implementation would be in client.ts
 */

import { GraphQLClient, type RequestDocument, type Variables } from 'graphql-request';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import { getGraphQLEndpoint, DEFAULT_ERROR_MESSAGE } from '$lib/config/system';
import { toast } from '$lib/components/venUI/toast';
import type { GraphQLError } from './types';

// Import generated types (example)
// import type { LoginMutation, LoginMutationVariables } from './generated/types';

/**
 * Enhanced GraphQL Query with Type Inference
 * 
 * Benefits:
 * - Types automatically inferred from TypedDocumentNode
 * - No manual type definitions needed
 * - Full type safety for variables and response
 * - IDE autocomplete support
 */
export async function graphqlQuery<
  TData = unknown,
  TVariables extends Variables = Variables
>(
  document: TypedDocumentNode<TData, TVariables> | RequestDocument,
  options: GraphQLQueryOptions<TVariables> = {}
): Promise<GraphQLResult<TData>> {
  // Implementation with automatic type inference
  // Types come from TypedDocumentNode, no manual definitions needed
  
  const { variables, cacheKey, cacheTTL, skipCache = false, skipLoading = false } = options;

  // Generate cache key from document
  const finalCacheKey = cacheKey || generateCacheKey(document, variables);

  // Check cache first
  if (!skipCache && finalCacheKey) {
    const cached = requestCache.get(finalCacheKey);
    if (cached) {
      return { success: true, data: cached as TData };
    }
  }

  // Check for pending request (deduplication)
  if (pendingRequests.has(finalCacheKey)) {
    const pendingData = await pendingRequests.get(finalCacheKey)!;
    return { success: true, data: pendingData as TData };
  }

  const client = createGraphQLClient();

  const requestPromise = (async () => {
    try {
      if (!skipLoading) {
        incrementLoading();
      }

      // Type-safe request - TData and TVariables inferred from document
      const data = await client.request<TData>(document, variables);

      if (!skipCache && finalCacheKey) {
        requestCache.set(finalCacheKey, data, cacheTTL);
      }

      return data;
    } catch (error) {
      pendingRequests.delete(finalCacheKey);
      throw error;
    } finally {
      if (!skipLoading) {
        decrementLoading();
      }
    }
  })();

  pendingRequests.set(finalCacheKey, requestPromise);

  try {
    const data = await requestPromise;
    pendingRequests.delete(finalCacheKey);
    return { success: true, data };
  } catch (error) {
    pendingRequests.delete(finalCacheKey);
    const graphqlErrors = extractGraphQLErrors(error);
    const errorMessage = normalizeErrorMessage(error);

    if (
      error &&
      typeof error === 'object' &&
      'response' in error &&
      (error as { response?: { status?: number } }).response?.status === 401
    ) {
      clearUserSession();
    }

    if (errorMessage) {
      toast.error(errorMessage);
    }

    return {
      success: false,
      error: errorMessage,
      errors: graphqlErrors,
      code: error && typeof error === 'object' && 'response' in error
        ? (error as { response?: { status?: number } }).response?.status
        : undefined
    };
  }
}

/**
 * Enhanced GraphQL Mutation with Type Inference
 */
export async function graphqlMutation<
  TData = unknown,
  TVariables = Record<string, unknown>
>(
  document: TypedDocumentNode<TData, TVariables> | RequestDocument,
  options: GraphQLMutationOptions<TVariables> = {}
): Promise<GraphQLResult<TData>> {
  const { variables, skipLoading = false } = options;
  const client = createGraphQLClient();

  try {
    if (!skipLoading) {
      incrementLoading();
    }

    // Type-safe mutation - types inferred from document
    const data = await client.request<TData>(document, variables as import('graphql-request').Variables);

    return { success: true, data };
  } catch (error) {
    const graphqlErrors = extractGraphQLErrors(error);
    const errorMessage = normalizeErrorMessage(error);

    if (
      error &&
      typeof error === 'object' &&
      'response' in error &&
      (error as { response?: { status?: number } }).response?.status === 401
    ) {
      clearUserSession();
    }

    if (errorMessage) {
      toast.error(errorMessage);
    }

    return {
      success: false,
      error: errorMessage,
      errors: graphqlErrors,
      code: error && typeof error === 'object' && 'response' in error
        ? (error as { response?: { status?: number } }).response?.status
        : undefined
    };
  } finally {
    if (!skipLoading) {
      decrementLoading();
    }
  }
}

// ============================================================================
// USAGE EXAMPLES
// ============================================================================

/**
 * Example 1: Using Generated Document Node
 * 
 * BEFORE (Current):
 * ```typescript
 * const loginMutation = buildMutation`
 *   mutation Login($username: String!, $password: String!) {
 *     login(input: { username: $username, password: $password }) {
 *       token
 *       username
 *     }
 *   }
 * `;
 * 
 * const result = await graphqlMutation<{
 *   login: { token: string; username: string; };
 * }>(loginMutation, {
 *   variables: {
 *     username: values.username,
 *     password: values.password
 *   }
 * });
 * ```
 * 
 * AFTER (With Code Generation):
 * ```typescript
 * import { LoginDocument } from '$lib/services/graphql/generated';
 * 
 * // No manual type definition needed!
 * // Types are automatically inferred from LoginDocument
 * const result = await graphqlMutation(LoginDocument, {
 *   variables: {
 *     username: values.username,  // ✅ Type-checked
 *     password: values.password   // ✅ Type-checked
 *   }
 * });
 * 
 * // result.data.login.token ✅ Fully typed
 * // result.data.login.username ✅ Fully typed
 * ```
 */

/**
 * Example 2: Using Operation Registry
 * 
 * ```typescript
 * import { Operations } from '$lib/services/graphql/operations';
 * 
 * const result = await graphqlMutation(Operations.mutations.login, {
 *   variables: {
 *     username: 'user',
 *     password: 'pass'
 *   }
 * });
 * ```
 */

/**
 * Example 3: With Hooks
 * 
 * ```svelte
 * <script lang="ts">
 *   import { useGraphQLMutation } from '$lib/services/graphql/hooks';
 *   import { Operations } from '$lib/services/graphql/operations';
 * 
 *   const { mutate, loading, error } = useGraphQLMutation(
 *     Operations.mutations.login
 *   );
 * 
 *   async function handleLogin() {
 *     const result = await mutate({
 *       username: 'user',  // ✅ Type-checked
 *       password: 'pass'    // ✅ Type-checked
 *     });
 * 
 *     if (result.success) {
 *       // result.data.login ✅ Fully typed
 *       console.log(result.data.login.token);
 *     }
 *   }
 * </script>
 * ```
 */

/**
 * Example 4: Query with Variables
 * 
 * ```typescript
 * import { GetUserDocument } from '$lib/services/graphql/generated';
 * 
 * const result = await graphqlQuery(GetUserDocument, {
 *   variables: {
 *     id: '123'  // ✅ Type-checked against schema
 *   },
 *   cacheKey: 'user-123'
 * });
 * 
 * // result.data.user ✅ Fully typed with all fields
 * ```
 */

// ============================================================================
// TYPE DEFINITIONS
// ============================================================================

export interface GraphQLQueryOptions<TVariables = Record<string, unknown>> {
  variables?: TVariables;
  cacheKey?: string;
  cacheTTL?: number;
  skipCache?: boolean;
  skipLoading?: boolean;
}

export interface GraphQLMutationOptions<TVariables = Record<string, unknown>> {
  variables?: TVariables;
  skipLoading?: boolean;
}

export interface GraphQLResult<TData = unknown> {
  success: boolean;
  data?: TData;
  error?: string;
  errors?: GraphQLError[];
  code?: number | string;
}

// Helper functions (would be in actual implementation)
function generateCacheKey(document: RequestDocument, variables?: unknown): string {
  const docString = typeof document === 'string' ? document : JSON.stringify(document);
  const varsString = variables ? JSON.stringify(variables) : '';
  return `gql:${docString}:${varsString}`;
}

function createGraphQLClient(): GraphQLClient {
  // Implementation
  return new GraphQLClient(getGraphQLEndpoint());
}

function extractGraphQLErrors(error: unknown): GraphQLError[] {
  // Implementation
  return [];
}

function normalizeErrorMessage(error: unknown): string {
  // Implementation
  return DEFAULT_ERROR_MESSAGE;
}

function clearUserSession(): void {
  // Implementation
}

function incrementLoading(): void {
  // Implementation
}

function decrementLoading(): void {
  // Implementation
}

const requestCache = {
  get: (key: string) => null,
  set: (key: string, data: unknown, ttl?: number) => {},
};

const pendingRequests = new Map<string, Promise<unknown>>();
