import { graphqlQuery } from "$lib/services/graphql";
import type { RequestDocument, Variables } from "graphql-request";
import { toast } from "$lib/components/venUI/toast";

type PaginationStrategy = "server" | "client" | "auto";

interface PaginationOptions<TVariables> {
  query: RequestDocument;
  variables?: TVariables;
  strategy?: PaginationStrategy;
  pageSize?: number;
  clientModeMaxLimit?: number;
  /** Root key (e.g. 'users'). Used to derive itemsPath/countPath when not set. */
  dataPath?: string;
  /** Dot-separated path to items array (e.g. 'users.items'). Defaults to `${dataPath}.items` if dataPath set. */
  itemsPath?: string;
  /** Dot-separated path to totalCount (e.g. 'users.totalCount'). Defaults to `${dataPath}.totalCount` if dataPath set. */
  countPath?: string;
  /** Skip GraphQL request cache so each load fetches fresh data. Use for lists that change (e.g. users). */
  skipCache?: boolean;
  /**
   * When set, search box merges this result into request variables instead of `searchTerm`.
   * Use for GraphQL fields that filter via `where` (e.g. Hot Chocolate UseFiltering).
   */
  mapSearchToVariables?: (term: string) => Partial<TVariables>;
}

export class SmartPagination<T, TVariables extends Variables = Variables> {
  // State
  items = $state<T[]>([]);
  totalCount = $state(0);
  loading = $state(false);
  /** True when fetching next page (append). Use to show inline loader instead of full skeletons. */
  loadingMore = $state(false);
  error = $state<string | undefined>(undefined);

  // Pagination State
  currentPage = $state(1);
  pageSize = $state(50);
  /** True when more pages exist. For client strategy, items = current page slice; for server, items = current page. */
  hasMore = $derived(this.currentPage * this.pageSize < this.totalCount);

  // Sort State
  sortField = $state<string | null>(null);
  sortDirection = $state<"asc" | "desc">("asc");

  // Internal
  private query: RequestDocument;
  private baseVariables: TVariables;
  private strategy: PaginationStrategy;
  private clientModeMaxLimit: number;
  private itemsPath: string;
  private countPath: string;
  private skipCache: boolean;
  private mapSearchToVariables?: (term: string) => Partial<TVariables>;

  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private requestId = 0;
  private allClientData: T[] = [];

  constructor(options: PaginationOptions<TVariables>) {
    this.query = options.query;
    this.baseVariables = options.variables || ({} as TVariables);
    this.strategy = options.strategy || "server";
    this.pageSize = options.pageSize || 50;
    this.clientModeMaxLimit = options.clientModeMaxLimit || 1000;
    this.skipCache = options.skipCache ?? false;
    this.itemsPath =
      options.itemsPath ??
      (options.dataPath ? `${options.dataPath}.items` : "");
    this.countPath =
      options.countPath ??
      (options.dataPath ? `${options.dataPath}.totalCount` : "");
    this.mapSearchToVariables = options.mapSearchToVariables;

    this.load();
  }

  setSearch(term: string) {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      if (this.mapSearchToVariables) {
        this.baseVariables = {
          ...this.baseVariables,
          ...this.mapSearchToVariables(term),
        } as TVariables;
      } else {
        this.baseVariables = { ...this.baseVariables, searchTerm: term };
      }
      this.reset();
      this.load();
    }, 300); // 300ms debounce
  }

  setVariables(vars: Partial<TVariables>) {
    this.baseVariables = { ...this.baseVariables, ...vars };
    this.reset();
    this.load();
  }

  toggleSort(field: string) {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === "asc" ? "desc" : "asc";
    } else {
      this.sortField = field;
      this.sortDirection = "asc";
    }
    this.reset();
    this.load();
  }

  sort(field: string, direction: "asc" | "desc") {
    this.sortField = field;
    this.sortDirection = direction;
    this.reset();
    this.load();
  }

  async nextPage() {
    if (this.loading || this.loadingMore || !this.hasMore) return;
    this.currentPage++;
    await this.load(true); // append = true
  }

  reset() {
    this.items = [];
    this.totalCount = 0;
    this.currentPage = 1;
    this.allClientData = [];
  }

  async load(append = false) {
    const currentRequestId = ++this.requestId;
    if (append) {
      this.loadingMore = true;
    } else {
      this.loading = true;
    }
    this.error = undefined;

    try {
      if (this.strategy === "client") {
        await this.loadClientMode(currentRequestId);
      } else {
        await this.loadServerMode(currentRequestId, append);
      }
    } catch (e: any) {
      if (this.requestId === currentRequestId) {
        this.error = e.message || "Failed to load data";
        toast.error(this.error!);
      }
    } finally {
      if (this.requestId === currentRequestId) {
        this.loading = false;
        this.loadingMore = false;
      }
    }
  }

  private async loadServerMode(reqId: number, append: boolean) {
    const skip = (this.currentPage - 1) * this.pageSize;

    const vars = {
      ...this.baseVariables,
      skip,
      take: this.pageSize,
      orderBy: this.sortField, // Backend must support this
      desc: this.sortDirection === "desc",
    };

    const result = await graphqlQuery(this.query, {
      variables: vars,
      skipCache: this.skipCache,
    });

    if (this.requestId !== reqId) return; // Stale request

    if (result.success && result.data) {
      const { items, count } = this.extractData(result.data);
      if (append) {
        this.items = [...this.items, ...items];
      } else {
        this.items = items;
      }
      this.totalCount = count;
    } else if (result.error) {
      this.error = result.error;
    }
  }

  private async loadClientMode(reqId: number) {
    // In client mode, we only fetch ONCE (page 1, big limit)
    // If we already have data and arguments (search) haven't changed,
    // we just slice. BUT if search changed, we refetch.

    // Optimization: If only SORT changed, don't refetch, just resort.
    // We need to track what caused the load.
    // For now, simpler: check if we have data.

    let itemsToProcess = this.allClientData;

    // Fetch if empty
    if (itemsToProcess.length === 0) {
      const vars = {
        ...this.baseVariables,
        skip: 0,
        take: this.clientModeMaxLimit,
      };

      const result = await graphqlQuery(this.query, {
        variables: vars,
        skipCache: this.skipCache,
      });

      if (this.requestId !== reqId) return;

      if (result.success && result.data) {
        const { items } = this.extractData(result.data);
        this.allClientData = items;
        itemsToProcess = items;
        this.totalCount = items.length;
      } else if (result.error) {
        this.error = result.error;
        return;
      }
    }

    // Apply Client-Side Sort
    if (this.sortField) {
      const field = this.sortField as keyof T;
      const dir = this.sortDirection === "asc" ? 1 : -1;
      itemsToProcess = [...itemsToProcess].sort((a, b) => {
        const valA = a[field];
        const valB = b[field];
        if (valA < valB) return -1 * dir;
        if (valA > valB) return 1 * dir;
        return 0;
      });
    }

    // Apply Client-Side Pagination
    const start = (this.currentPage - 1) * this.pageSize;
    const end = start + this.pageSize;
    this.items = itemsToProcess.slice(start, end);
  }

  private applyClientSlice() {
    // Deprecated by loadClientMode logic above which generally handles the flow
    // But if we need to re-slice without loading:
    // We'd need to invoke loadClientMode again.
  }

  private getByPath(obj: unknown, path: string): unknown {
    if (!obj || typeof obj !== "object" || !path) return undefined;
    const parts = path.split(".");
    let cur: unknown = obj;
    for (const p of parts) {
      if (cur == null || typeof cur !== "object") return undefined;
      cur = (cur as Record<string, unknown>)[p];
    }
    return cur;
  }

  private extractData(data: unknown): { items: T[]; count: number } {
    try {
      if (this.itemsPath && this.countPath) {
        const items = this.getByPath(data, this.itemsPath);
        const count = this.getByPath(data, this.countPath);
        const arr = Array.isArray(items) ? items : [];
        const num = typeof count === "number" ? count : 0;
        return { items: arr as T[], count: num };
      }
      if (!data || typeof data !== "object") return { items: [], count: 0 };
      const keys = Object.keys(data as object);
      if (keys.length === 1) {
        const root = (data as Record<string, unknown>)[keys[0]];
        if (root && typeof root === "object" && "items" in root) {
          const r = root as { items?: unknown[]; totalCount?: number };
          return {
            items: (r.items ?? []) as T[],
            count: typeof r.totalCount === "number" ? r.totalCount : 0,
          };
        }
      }
      return { items: [], count: 0 };
    } catch {
      return { items: [], count: 0 };
    }
  }
}
