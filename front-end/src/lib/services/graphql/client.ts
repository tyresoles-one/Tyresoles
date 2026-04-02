import {
  GraphQLClient,
  type RequestDocument,
  type Variables,
} from "graphql-request";
import type { TypedDocumentNode } from "@graphql-typed-document-node/core";
import { getGraphQLEndpoint } from "$lib/config/system";
import { handleGraphQLError } from "./config";
import type { GraphQLError } from "./types";
import { getAuthToken as getStoreToken, clearAuth } from "$lib/stores/auth";
import stableStringify from "fast-json-stable-stringify";
import { browser } from "$app/environment";
import { goto } from "$app/navigation";
import { Toast } from "$lib/components/venUI/toast";
import { setGraphQLConfig } from "./config";
import { clearUserSession } from "$lib/services/auth/session";

/**
 * GraphQL Error class for better error handling
 */
export class GraphQLNetworkError extends Error {
  constructor(
    public message: string,
    public code?: number | string,
    public errors?: GraphQLError[],
    public data?: unknown,
  ) {
    super(message);
    this.name = "GraphQLNetworkError";
  }
}

/**
 * Request cache for query deduplication and performance
 * Optimized with LRU eviction and request deduplication
 */
class RequestCache {
  private cache = new Map<string, { data: unknown; timestamp: number }>();
  private readonly TTL = 5 * 60 * 1000; // 5 minutes default TTL
  private readonly MAX_SIZE = 100; // Maximum cache entries
  private accessOrder: string[] = []; // For LRU eviction

  set(key: string, data: unknown, ttl?: number): void {
    // Evict oldest entry if cache is full
    if (this.cache.size >= this.MAX_SIZE && !this.cache.has(key)) {
      const oldestKey = this.accessOrder.shift();
      if (oldestKey) {
        this.cache.delete(oldestKey);
      }
    }

    // Update access order
    const index = this.accessOrder.indexOf(key);
    if (index > -1) {
      this.accessOrder.splice(index, 1);
    }
    this.accessOrder.push(key);

    this.cache.set(key, {
      data,
      timestamp: Date.now() + (ttl || this.TTL),
    });
  }

  get(key: string): unknown | null {
    const cached = this.cache.get(key);
    if (!cached) return null;

    if (Date.now() > cached.timestamp) {
      this.cache.delete(key);
      const index = this.accessOrder.indexOf(key);
      if (index > -1) {
        this.accessOrder.splice(index, 1);
      }
      return null;
    }

    // Update access order (move to end)
    const index = this.accessOrder.indexOf(key);
    if (index > -1) {
      this.accessOrder.splice(index, 1);
      this.accessOrder.push(key);
    }

    return cached.data;
  }

  clear(): void {
    this.cache.clear();
    this.accessOrder = [];
  }

  delete(key: string): void {
    this.cache.delete(key);
    const index = this.accessOrder.indexOf(key);
    if (index > -1) {
      this.accessOrder.splice(index, 1);
    }
  }
}

const requestCache = new RequestCache();

/**
 * Request deduplication - prevents duplicate concurrent requests
 */
const pendingRequests = new Map<string, Promise<unknown>>();

/**
 * Generate cache key from document and variables
 */
function generateCacheKey(
  document: RequestDocument,
  variables?: unknown,
): string {
  const docString =
    typeof document === "string" ? document : stableStringify(document);
  const varsString = variables ? stableStringify(variables) : "";
  return `gql:${docString}:${varsString}`;
}

import { NativeTokenStore } from '$lib/utils/native-token-store';

/**
 * Get authentication token from storage or context (Async for Native Support)
 */
async function getAuthToken(): Promise<string> {
  if (typeof window === "undefined") return "";

  try {
    const nativeToken = await NativeTokenStore.getToken();
    if (nativeToken) return nativeToken;
    // Fallback to store
    return getStoreToken();
  } catch {
    // Ignore errors
  }

  return "";
}

export { clearUserSession };

// Register global unauthorized handler
setGraphQLConfig({
  onUnauthorized: clearUserSession
});

/**
 * Create GraphQL client with authentication and error handling
 */
async function createGraphQLClient(): Promise<GraphQLClient> {
  const endpoint = getGraphQLEndpoint();
  const token = await getAuthToken();

  const client = new GraphQLClient(endpoint, {
    headers: {
      "Content-Type": "application/json",
      Authorization: token ? `Bearer ${token}` : "",
    },
    credentials: "omit",
    mode: "cors",
    // Ensure proper CORS handling
    fetch: typeof window !== "undefined" ? window.fetch : undefined,
  });

  return client;
}

/**
 * GraphQL Query Options
 */
export interface GraphQLQueryOptions<TVariables = Variables> {
  variables?: TVariables;
  cacheKey?: string;
  cacheTTL?: number;
  skipCache?: boolean;
  skipLoading?: boolean;
  silent?: boolean;
}

/**
 * GraphQL Mutation Options
 */
export interface GraphQLMutationOptions<
  TVariables = Variables | Record<string, unknown>,
> {
  variables?: TVariables;
  skipLoading?: boolean;
  silent?: boolean;
}

/**
 * GraphQL Request Result
 */
export interface GraphQLResult<TData = unknown> {
  success: boolean;
  data?: TData;
  error?: string;
  errors?: GraphQLError[];
  code?: number | string;
}

/**
 * Loading state management
 * Uses a simple counter that works in both SSR and client environments
 * Note: This is a module-level variable, not reactive. Components should use
 * the store or subscribeLoading function for reactive access.
 */
let loadingCount = 0;

/**
 * Get current loading state
 */
export function isLoading(): boolean {
  return loadingCount > 0;
}

/**
 * Increment loading counter
 */
function incrementLoading(): void {
  loadingCount++;
}

/**
 * Decrement loading counter
 */
function decrementLoading(): void {
  loadingCount = Math.max(0, loadingCount - 1);
}

/**
 * Subscribe to loading state changes using $effect
 * SSR compatible - only subscribes on client side
 * More efficient than polling - uses Svelte's reactivity system
 */
export function subscribeLoading(
  callback: (loading: boolean) => void,
): () => void {
  if (typeof window === "undefined") {
    // SSR: return no-op unsubscribe
    return () => {};
  }

  // Use $effect to reactively track loadingCount changes
  // This is more efficient than polling with requestAnimationFrame
  let unsubscribe: (() => void) | null = null;

  // Create a reactive effect that tracks loadingCount
  // Note: This requires the callback to be called within a reactive context
  // For maximum compatibility, we'll use a hybrid approach
  let previousValue = loadingCount;
  callback(loadingCount > 0);

  // Use a more efficient approach: track changes directly
  // Since loadingCount is now $state, we can use a simple check
  const check = () => {
    const currentValue = loadingCount;
    if (currentValue !== previousValue) {
      previousValue = currentValue;
      callback(currentValue > 0);
    }
  };

  // Use requestAnimationFrame for efficient polling
  // This is still needed because $state changes need to be observed
  // In a future optimization, we could use a custom store-like interface
  let rafId: number;
  const poll = () => {
    check();
    rafId = requestAnimationFrame(poll);
  };
  rafId = requestAnimationFrame(poll);

  return () => {
    if (rafId) {
      cancelAnimationFrame(rafId);
    }
  };
}

/**
 * Execute a GraphQL query with caching, deduplication, and error handling
 */
export async function graphqlQuery<
  TData = unknown,
  TVariables extends Variables = Variables,
>(
  document: RequestDocument,
  options: GraphQLQueryOptions<TVariables> = {},
): Promise<GraphQLResult<TData>> {
  const {
    variables,
    cacheKey,
    cacheTTL,
    skipCache = false,
    skipLoading = false,
    silent = false,
  } = options;

  // Generate cache key if not provided
  const finalCacheKey = cacheKey || generateCacheKey(document, variables);

  // Check cache first (only for queries, not mutations)
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

  // Create request promise for deduplication
  const requestPromise = (async () => {
    try {
      if (!skipLoading) {
        incrementLoading();
      }

      const client = await createGraphQLClient();

      // Cast to RequestOptions to satisfy TS with generic variables
      const requestOptions = {
        document,
        variables: variables as TVariables,
      } as any;

      const data = await client.request<TData, TVariables>(requestOptions);

      // Cache the result if cache key is provided
      if (!skipCache && finalCacheKey) {
        requestCache.set(finalCacheKey, data, cacheTTL);
      }

      return data;
    } catch (error) {
      // Remove from pending on error
      pendingRequests.delete(finalCacheKey);
      throw error;
    } finally {
      if (!skipLoading) {
        decrementLoading();
      }
    }
  })();

  // Store pending request
  pendingRequests.set(finalCacheKey, requestPromise);

  try {
    const data = await requestPromise;
    pendingRequests.delete(finalCacheKey);
    return { success: true, data };
  } catch (error) {
    pendingRequests.delete(finalCacheKey);
    const { message, errors: graphqlErrors, code } = handleGraphQLError(error, {
      silent,
    });

    return {
      success: false,
      error: message,
      errors: graphqlErrors,
      code,
    };
  }
}

/**
 * Hot Chocolate often returns HTTP 200 with `errors: null` while a field resolver
 * returns `{ success: false, message }` (e.g. NAV/WCF failures). Treat that as failure
 * so callers (toasts, forms) see the real message instead of a false success.
 */
function extractMutationResultFailureMessage(data: unknown): string | undefined {
  if (data === null || data === undefined || typeof data !== "object") {
    return undefined;
  }
  const root = data as Record<string, unknown>;
  for (const key of Object.keys(root)) {
    const val = root[key];
    if (val !== null && typeof val === "object" && "success" in val) {
      const mr = val as { success?: unknown; message?: unknown };
      if (mr.success === false) {
        if (typeof mr.message === "string" && mr.message.trim() !== "") {
          return mr.message;
        }
        return "Operation failed";
      }
    }
  }
  return undefined;
}

/**
 * Execute a GraphQL mutation with error handling
 * Mutations are never cached or deduplicated
 * Supports both TypedDocumentNode (from codegen) and RequestDocument (legacy)
 */
export async function graphqlMutation<
  TData = unknown,
  TVariables extends Variables = Variables,
>(
  document: TypedDocumentNode<TData, TVariables> | RequestDocument,
  options: GraphQLMutationOptions<TVariables> = {},
): Promise<GraphQLResult<TData>> {
  const { variables, skipLoading = false, silent = false } = options;

  try {
    const client = await createGraphQLClient();

    if (!skipLoading) {
      incrementLoading();
    }

    // Cast to RequestOptions to satisfy TS with generic variables
    const requestOptions = {
      document,
      variables: variables as TVariables,
    } as any;

    const data = await client.request<TData, TVariables>(requestOptions);

    const failureMessage = extractMutationResultFailureMessage(data);
    if (failureMessage !== undefined) {
      return { success: false, error: failureMessage, data };
    }

    return { success: true, data };
  } catch (error) {
    const { message, errors: graphqlErrors, code } = handleGraphQLError(error, {
      silent,
    });
    return {
      success: false,
      error: message,
      errors: graphqlErrors,
      code,
    };
  } finally {
    if (!skipLoading) {
      decrementLoading();
    }
  }
}

/**
 * Clear GraphQL request cache
 */
export function clearGraphQLCache(cacheKey?: string): void {
  if (cacheKey) {
    requestCache.delete(cacheKey);
  } else {
    requestCache.clear();
  }
}

/**
 * Get raw GraphQL client (for advanced use cases)
 */
export async function getGraphQLClient(): Promise<GraphQLClient> {
  return await createGraphQLClient();
}
