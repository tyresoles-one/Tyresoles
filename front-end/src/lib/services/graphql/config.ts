/**
 * Global GraphQL config and error pipeline.
 *
 * All GraphQL errors go through the global handler. Set `showErrorToasts: false`
 * or `onError` in config to customize. Use `silent: true` on a request to skip
 * toast for that call.
 */

import { toast } from "$lib/components/venUI/toast";
import { getFriendlyError } from "$lib/utils";
import type { GraphQLError } from "./types";

export interface GraphQLErrorContext {
  errors?: GraphQLError[];
  code?: number | string;
  silent?: boolean;
}

export interface GraphQLConfig {
  /** When true (default), show a toast for GraphQL errors unless the request used silent: true. */
  showErrorToasts: boolean;
  /** Custom handler for every GraphQL error (logging, analytics, custom UI). */
  onError?: (normalizedMessage: string, context?: GraphQLErrorContext) => void;
  /** Override how the error message is derived from the raw error. */
  getErrorMessage?: (error: unknown) => string;
  /** Global hook for unauthorized errors (401 or UNAUTHENTICATED code). */
  onUnauthorized?: () => void;
}

const defaultConfig: GraphQLConfig = {
  showErrorToasts: true,
};

let graphQLConfig: GraphQLConfig = { ...defaultConfig };

/**
 * Update global GraphQL config. Pass a partial object to merge with current config.
 */
export function setGraphQLConfig(partial: Partial<GraphQLConfig>): void {
  graphQLConfig = { ...graphQLConfig, ...partial };
}

/**
 * Get current GraphQL config (read-only).
 */
export function getGraphQLConfig(): Readonly<GraphQLConfig> {
  return graphQLConfig;
}

/**
 * Notify adapter: show error toast only when global config allows it.
 * Use this for GraphQL errors so behavior is consistent and configurable.
 */
export function notifyError(message: string): void {
  if (graphQLConfig.showErrorToasts && message) {
    toast.error(message);
  }
}

function extractGraphQLErrors(error: unknown): GraphQLError[] {
  if (error && typeof error === "object" && "response" in error) {
    const response = (error as { response?: { errors?: GraphQLError[] } }).response;
    if (response?.errors) {
      return response.errors;
    }
  }
  return [];
}

/** Hot Chocolate / .NET often hide the real failure behind a generic top-level `message`. */
const GENERIC_GQL_TOP_MESSAGES = /^(Unexpected Execution Error|Internal Server Error)$/i;

/**
 * Walk .NET-style exception payloads (Hot Chocolate, Connector, WCF) for the innermost useful text.
 */
function extractFromExceptionTree(obj: unknown, depth: number): string | undefined {
  if (depth > 6 || obj === null || typeof obj !== "object") return undefined;
  const o = obj as Record<string, unknown>;
  const msg = o.message;
  if (typeof msg === "string") {
    const t = msg.trim();
    if (t && !GENERIC_GQL_TOP_MESSAGES.test(t)) return t;
  }
  const inner =
    o.innerException ?? o.InnerException ?? o.inner ?? o.Inner ?? o.cause;
  const nested = extractFromExceptionTree(inner, depth + 1);
  if (nested) return nested;
  if (typeof msg === "string" && msg.trim()) return msg.trim();
  return undefined;
}

/**
 * Prefer `extensions` (connector / resolver detail) over the generic GraphQL `message`.
 * Exported so callers can show the same text in custom UI (e.g. procurement line save).
 */
export function extractBestGraphQLErrorMessage(err: GraphQLError): string {
  const ext = err.extensions;
  if (ext && typeof ext === "object") {
    const ex = ext.exception ?? ext.Exception;
    const fromTree = extractFromExceptionTree(ex, 0);
    if (fromTree) return fromTree;

    if (typeof ext.message === "string") {
      const m = ext.message.trim();
      if (m && m !== err.message) return m;
      if (m && GENERIC_GQL_TOP_MESSAGES.test(String(err.message)) && !GENERIC_GQL_TOP_MESSAGES.test(m)) {
        return m;
      }
    }

    for (const key of ["detail", "detailMessage", "connectorMessage", "reason", "description"]) {
      const v = ext[key];
      if (typeof v === "string" && v.trim()) return v.trim();
    }

    const nested = ext.errors;
    if (Array.isArray(nested) && nested.length > 0) {
      const first = nested[0];
      if (typeof first === "string" && first.trim()) return first.trim();
      if (first && typeof first === "object" && "message" in first) {
        const fm = (first as { message?: unknown }).message;
        if (typeof fm === "string" && fm.trim()) return fm.trim();
      }
    }
  }

  const top = String(err.message ?? "").trim();
  if (top) return top;
  return "An unknown error occurred";
}

function getStatusCode(error: unknown): number | undefined {
  if (error && typeof error === "object" && "response" in error) {
    return (error as { response?: { status?: number } }).response?.status;
  }
  return undefined;
}

/**
 * Process a GraphQL error through the global pipeline: normalize message,
 * call config.onError, and optionally show toast. Does not clear session (client
 * does that on 401). Returns normalized message and context for building GraphQLResult.
 */
export function handleGraphQLError(
  error: unknown,
  options?: { silent?: boolean }
): { message: string; errors: GraphQLError[]; code?: number | string } {
  const errors = extractGraphQLErrors(error);
  const code = getStatusCode(error);
  const rawErrorMessage = errors.length > 0 && errors[0].message ? errors[0].message : String(error);
  
  const isUnauthorized = 
    code === 401 || 
    rawErrorMessage.includes(' 401') || 
    rawErrorMessage.includes(': 401') ||
    String(error).includes('401') ||
    errors.some(e => 
      e.extensions?.code === 'UNAUTHENTICATED' || 
      e.message?.toLowerCase().includes('unauthorized') ||
      e.message?.toLowerCase().includes('unauthenticated')
    );

  const message = graphQLConfig.getErrorMessage
    ? graphQLConfig.getErrorMessage(error)
    : errors.length > 0
      ? errors.length === 1
        ? extractBestGraphQLErrorMessage(errors[0])
        : errors.map((e) => extractBestGraphQLErrorMessage(e)).join(" · ")
      : getFriendlyError(error);

  const context: GraphQLErrorContext = {
    errors,
    code,
    silent: options?.silent,
  };

  graphQLConfig.onError?.(message, context);

  if (isUnauthorized) {
    console.warn("[GraphQL] Unauthorized access detected", { code, message, error });
    if (graphQLConfig.onUnauthorized) {
      graphQLConfig.onUnauthorized();
    }
  }

  if (message && !options?.silent && graphQLConfig.showErrorToasts) {
    toast.error(message);
  }

  return { message, errors, code };
}
