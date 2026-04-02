import { onMount } from "svelte";
import { graphqlQuery } from "./client";
import { notifyError } from "./config";
import type { RequestDocument, Variables } from "graphql-request";
import type { TypedDocumentNode } from "@graphql-typed-document-node/core";

/**
 * Paginated list shape aligned with backend PaginatedResponse<T>.
 */
export interface PaginatedListResult<T> {
  items: T[];
  totalCount: number;
  hasNextPage?: boolean;
}

/**
 * Config for a paginated list query. Use with usePaginatedQuery.
 */
export interface ListQueryConfig<T, TVariables extends Variables = Variables> {
  /** GraphQL document (query) for the list. */
  document:
    | RequestDocument
    | TypedDocumentNode<{ [k: string]: PaginatedListResult<T> }, TVariables>;
  /** Root key in response (e.g. 'users'). Used to resolve items/count when paths not set. */
  dataPath?: string;
  /** Dot-separated path to items array (e.g. 'users.items'). */
  itemsPath?: string;
  /** Dot-separated path to totalCount (e.g. 'users.totalCount'). */
  countPath?: string;
  /** Initial variables (searchTerm, state, skip, take, etc.). */
  variables?: TVariables;
  /** 'server' = fetch each page; 'client' = fetch once, slice locally. */
  strategy?: "server" | "client";
  pageSize?: number;
  clientModeMaxLimit?: number;
}

export interface UsePaginatedQueryResult<
  T,
  TVariables extends Variables = Variables,
> {
  items: T[];
  totalCount: number;
  loading: boolean;
  error: string | undefined;
  hasMore: boolean;
  nextPage: () => Promise<void>;
  refetch: () => Promise<void>;
  setSearch: (term: string) => void;
  setVariables: (vars: Partial<TVariables>) => void;
}

function getByPath(obj: unknown, path: string): unknown {
  if (!obj || typeof obj !== "object") return undefined;
  const parts = path.split(".");
  let cur: unknown = obj;
  for (const p of parts) {
    if (cur == null || typeof cur !== "object") return undefined;
    cur = (cur as Record<string, unknown>)[p];
  }
  return cur;
}

function extractPaginated<T>(
  data: unknown,
  itemsPath: string,
  countPath: string,
): { items: T[]; count: number } {
  try {
    const items = getByPath(data, itemsPath);
    const count = getByPath(data, countPath);
    const arr = Array.isArray(items) ? items : [];
    const num = typeof count === "number" ? count : 0;
    return { items: arr as T[], count: num };
  } catch {
    return { items: [], count: 0 };
  }
}

function autoDetectPaths(
  data: unknown,
): { itemsPath: string; countPath: string } | null {
  if (!data || typeof data !== "object") return null;
  const keys = Object.keys(data as object);
  if (keys.length !== 1) return null;
  const root = (data as Record<string, unknown>)[keys[0]];
  if (!root || typeof root !== "object") return null;
  const r = root as Record<string, unknown>;
  if (!("items" in r) || !Array.isArray(r.items)) return null;
  const base = keys[0];
  return { itemsPath: `${base}.items`, countPath: `${base}.totalCount` };
}

/**
 * createListQueryConfig normalizes and fills defaults for list query config.
 */
export function createListQueryConfig<
  T,
  TVariables extends Variables = Variables,
>(config: ListQueryConfig<T, TVariables>): ListQueryConfig<T, TVariables> {
  return {
    strategy: "server",
    pageSize: 50,
    clientModeMaxLimit: 1000,
    ...config,
  };
}

/**
 * usePaginatedQuery: generic paginated list hook over GraphQL.
 * Uses config's itemsPath/countPath (or dataPath) to extract items and totalCount.
 * Supports server-side and client-side pagination.
 */
export function usePaginatedQuery<T, TVariables extends Variables = Variables>(
  config: ListQueryConfig<T, TVariables>,
): UsePaginatedQueryResult<T, TVariables> {
  const cfg = createListQueryConfig(config);
  const itemsPath =
    cfg.itemsPath ?? (cfg.dataPath ? `${cfg.dataPath}.items` : undefined);
  const countPath =
    cfg.countPath ?? (cfg.dataPath ? `${cfg.dataPath}.totalCount` : undefined);

  let baseVariables = $state(cfg.variables ?? ({} as TVariables)) as TVariables;
  let items = $state<T[]>([]) as T[];
  let totalCount = $state(0);
  let loading = $state(false);
  let error = $state<string | undefined>(undefined);
  let currentPage = $state(1);
  const pageSize = cfg.pageSize ?? 50;
  const strategy = cfg.strategy ?? "server";
  const clientModeMaxLimit = cfg.clientModeMaxLimit ?? 1000;
  let allClientData = $state<T[]>([]) as T[];
  let searchTimer: ReturnType<typeof setTimeout> | null = null;
  let requestId = 0;

  const hasMore = $derived(
    strategy === "client"
      ? currentPage * pageSize < totalCount
      : items.length < totalCount,
  );

  function extract(data: unknown): { items: T[]; count: number } {
    if (itemsPath && countPath)
      return extractPaginated<T>(data, itemsPath, countPath);
    const detected = autoDetectPaths(data);
    if (detected)
      return extractPaginated<T>(data, detected.itemsPath, detected.countPath);
    return { items: [], count: 0 };
  }

  async function load(append = false) {
    const reqId = ++requestId;
    loading = true;
    error = undefined;

    try {
      if (strategy === "client") {
        let toProcess = allClientData;
        if (toProcess.length === 0) {
          const vars = {
            ...baseVariables,
            skip: 0,
            take: clientModeMaxLimit,
          } as TVariables;
          const result = await graphqlQuery(cfg.document, {
            variables: vars,
            skipLoading: true,
          });
          if (requestId !== reqId) return;
          if (result.success && result.data) {
            const { items: list } = extract(result.data);
            allClientData = list;
            toProcess = list;
            totalCount = list.length;
          } else {
            error = result.error;
            return;
          }
        }
        // Client-side pagination
        const start = (currentPage - 1) * pageSize;
        items = toProcess.slice(start, start + pageSize);
      } else {
        const skip = (currentPage - 1) * pageSize;
        const vars = { ...baseVariables, skip, take: pageSize } as TVariables;
        const result = await graphqlQuery(cfg.document, {
          variables: vars,
          skipLoading: true,
        });
        if (requestId !== reqId) return;
        if (result.success && result.data) {
          const { items: list, count } = extract(result.data);
          if (append) items = [...items, ...list];
          else items = list;
          totalCount = count;
        } else {
          error = result.error;
        }
      }
    } catch (e: unknown) {
      if (requestId === reqId) {
        error = (e as Error)?.message ?? "Failed to load";
        notifyError(error);
      }
    } finally {
      if (requestId === reqId) loading = false;
    }
  }

  function reset() {
    items = [];
    totalCount = 0;
    currentPage = 1;
    allClientData = [];
  }

  function setSearch(term: string) {
    if (searchTimer) clearTimeout(searchTimer);
    searchTimer = setTimeout(() => {
      baseVariables = { ...baseVariables, searchTerm: term } as TVariables;
      reset();
      load();
    }, 300);
  }

  function setVariables(vars: Partial<TVariables>) {
    baseVariables = { ...baseVariables, ...vars } as TVariables;
    reset();
    load();
  }

  async function nextPage() {
    if (loading || !hasMore) return;
    currentPage++;
    await load(true);
  }

  async function refetch() {
    reset();
    await load();
  }

  onMount(() => {
    load();
    return () => {
      if (searchTimer) clearTimeout(searchTimer);
    };
  });

  return {
    get items() {
      return items;
    },
    get totalCount() {
      return totalCount;
    },
    get loading() {
      return loading;
    },
    get error() {
      return error;
    },
    get hasMore() {
      return hasMore;
    },
    nextPage,
    refetch,
    setSearch,
    setVariables,
  };
}
