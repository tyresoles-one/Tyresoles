import { createClient, type ClientOptions, type Sink } from 'graphql-ws';
import { getGraphQLEndpointWs } from '$lib/config/system';
import { getAuthToken } from '$lib/stores/auth';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import type { Variables } from 'graphql-request';

let client: ReturnType<typeof createClient> | null = null;

function getClient() {
  if (typeof window === 'undefined') return null;
  if (client) return client;

  const options: ClientOptions = {
    url: getGraphQLEndpointWs(),
    connectionParams: async () => {
      const token = getAuthToken();
      return {
        Authorization: token ? `Bearer ${token}` : '',
      };
    },
    /**
     * Do not force retry on every error type. The previous `shouldRetry: () => true` +
     * `retryAttempts: Infinity` caused endless reconnect loops when the HTTP→WS upgrade
     * failed (e.g. 503 from the load balancer), spamming the console.
     * Defaults: retry transient socket closes only, bounded attempts.
     */
    shouldRetry: (errOrCloseEvent) => {
      if (!getAuthToken()) return false;
      return typeof CloseEvent !== "undefined" && errOrCloseEvent instanceof CloseEvent;
    },
  };

  client = createClient(options);
  return client;
}

/**
 * Subscribe to a GraphQL subscription
 * @param document The subscription document
 * @param variables Variables for the subscription
 * @param sink Callbacks for the subscription data, errors and completion
 * @returns Unsubscribe function
 */
export function subscribe<TData = unknown, TVariables extends Variables = Variables>(
  document: TypedDocumentNode<TData, TVariables>,
  variables: TVariables,
  sink: Sink<TData>
): () => void {
  const wsClient = getClient();
  if (!wsClient) {
    sink.error?.(new Error('WebSocket client not available (SSR)'));
    return () => {};
  }

  return wsClient.subscribe(
    {
      query: (document as any).loc?.source.body || document.toString(),
      variables: variables as any,
    },
    sink as any
  );
}
