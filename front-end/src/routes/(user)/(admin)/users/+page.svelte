<script lang="ts">
  import { goto } from "$app/navigation";

  // Reusable composables & components
  import { usePaginatedList } from "$lib/composables";
  import { TableActions } from "$lib/components/venUI/tableActions";
  import { StatusBadge } from "$lib/components/venUI/statusBadge";

  // UI
  import { Card, CardContent } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import {
    Avatar,
    AvatarFallback,
    AvatarImage,
  } from "$lib/components/ui/avatar";
  import { Icon } from "$lib/components/venUI/icon";
  import { Dropdown } from "$lib/components/venUI/dropdowns";
  import { TableCell, TableHead } from "$lib/components/ui/table";
  import MasterList from "$lib/components/venUI/masterList/MasterList.svelte";
  import { toast } from "$lib/components/venUI/toast";

  // GraphQL & utils
  import { gql } from "graphql-request";
  import { getGraphQLClient } from "$lib/services/graphql/client";
  import { getBackendBaseUrl } from "$lib/config/system";
  import { authStore } from "$lib/stores/auth";
  import { get } from "svelte/store";
  
  const GetUsersDocument: any = gql`
    query GetUsers($skip: Int, $take: Int, $where: UserFilterInput, $order: [UserSortInput!], $duplicateMobileOnly: Boolean) {
      users(skip: $skip, take: $take, where: $where, order: $order, duplicateMobileOnly: $duplicateMobileOnly) {
        items {
          userSecurityID
          userName
          fullName
          userType
          mobileNo
          authenticationEmail
          state
          avatar
        }
        totalCount
      }
    }
  `;

  const GetDriveSyncAdminStatusesDocument: any = gql`
    query GetDriveSyncAdminStatuses(
      $userIds: [String!]!
      $includeFolderValidation: Boolean!
      $includeUsage: Boolean!
      $includeLatestBackup: Boolean!
    ) {
      getDriveSyncAdminStatuses(
        userIds: $userIds
        includeFolderValidation: $includeFolderValidation
        includeUsage: $includeUsage
        includeLatestBackup: $includeLatestBackup
      ) {
        userId
        isUserFound
        isActive
        targetFolderId
        folderValidated
        folderValidationError
        quotaBytes
        usedBytes
        usageError
        latestBackupUtc
        latestBackupError
        lastCheckedUtc
      }
    }
  `;

  type GetUsersQuery = {
    users: {
      items: Array<{
        userSecurityId?: string;
        userName: string;
        fullName: string;
        userType: string;
        mobileNo?: string;
        authenticationEmail?: string;
        state: number;
        avatar?: string;
      }>;
      totalCount: number;
    };
  };
  type DriveSyncAdminStatus = {
    userId: string;
    isUserFound: boolean;
    isActive: boolean;
    targetFolderId: string;
    folderValidated?: boolean | null;
    folderValidationError?: string | null;
    quotaBytes: number | string;
    usedBytes: number | string;
    usageError?: string | null;
    latestBackupUtc?: string | null;
    latestBackupError?: string | null;
    lastCheckedUtc: string;
  };
  type DriveSyncOAuthStatus = {
    isConfigured: boolean;
    hasRefreshToken: boolean;
    hasAccessToken: boolean;
    accessTokenExpiryUtc?: string | null;
    isAccessTokenExpired: boolean;
    googleAccountEmail?: string | null;
    updatedAtUtc?: string | null;
    updatedByUserId?: string | null;
  };

  import { cn, getInitials } from "$lib/utils";

  type User = NonNullable<GetUsersQuery["users"]>["items"][number];
  type ViewMode = "grid" | "table";

  const USER_TYPES: { code: string; name: string }[] = [
    { code: "ACCOUNTS", name: "Accounts" },
    { code: "ADMIN", name: "Admin" },
    { code: "CUSTCARE", name: "Customer Care" },
    { code: "DEALER", name: "Dealer" },
    { code: "HOACCT", name: "HO Accounts" },
    { code: "HOHR", name: "HO HR" },
    { code: "MGMT", name: "Management" },
    { code: "PRODMGMT", name: "Production Management" },
    { code: "SALES", name: "Sales" },
    { code: "SUPER", name: "Super" },
  ];

  let viewMode = $state<ViewMode>("grid");
  let statusFilter = $state<"All" | "Active" | "Inactive">("All");
  let userTypeFilter = $state<string>("");
  let showDuplicateMobileOnly = $state(false);
  let driveSyncStatusByUser = $state<Record<string, DriveSyncAdminStatus>>({});
  let driveSyncStatusLoading = $state(false);
  let driveSyncStatusLoaded = $state(false);
  let driveSyncStatusError = $state<string | null>(null);
  let oauthStatus = $state<DriveSyncOAuthStatus | null>(null);
  let oauthLoading = $state(false);

  const list = usePaginatedList<User>({
    query: GetUsersDocument,
    dataPath: "users",
    pageSize: 50,
    manualSearch: true,
    serverVariableAllowlist: ["where", "order", "duplicateMobileOnly", "skip", "take"],
    mapSearchToVariables: (term) => {
      if (!term) return { where: undefined };
      return {
        where: {
          or: [
            { fullName: { contains: term } },
            { userName: { contains: term } },
            { mobileNo: { contains: term } },
          ],
        },
      };
    },
  });

  import { untrack } from "svelte";

  let lastState = $state<number | null | "init">("init");
  let lastUserType = $state<string | undefined | "init">("init");
  let lastDuplicateMobileOnly = $state<true | undefined | "init">("init");
  let lastSort = $state<string | null>("init");

  let isInitialLoad = true;

  $effect(() => {
    // Reactive inputs - these trigger the effect
    const stateVal = statusFilter === "Active" ? 0 : statusFilter === "Inactive" ? 1 : null;
    const utFilter = userTypeFilter || undefined;
    const dupOnly = showDuplicateMobileOnly ? true : undefined;
    const q = list.searchQuery.value;
    const sField = list.pagination.sortField;
    const sDir = list.pagination.sortDirection;

    untrack(() => {
      // Construction of complex objects
      const filters: any[] = [];
      if (stateVal !== null) filters.push({ state: { eq: stateVal } });
      if (utFilter) filters.push({ userType: { eq: utFilter } });
      
      const searchVars = q ? list.pagination.mapSearchToVariables?.(q) : null;
      if (searchVars?.where) filters.push(searchVars.where);

      const where = filters.length > 0 ? { and: filters } : undefined;
      const order = sField ? [{ [sField]: sDir === "asc" ? "ASC" : "DESC" }] : undefined;

      // Skip update if variables are identical to current ones
      const current = list.pagination.baseVariables;
      const hasChanged = 
        JSON.stringify(where) !== JSON.stringify(current.where) ||
        dupOnly !== current.duplicateMobileOnly ||
        JSON.stringify(order) !== JSON.stringify(current.order);

      if (!hasChanged && !isInitialLoad) return;
      isInitialLoad = false;

      list.pagination.setVariables({ 
        where, 
        order, 
        duplicateMobileOnly: dupOnly 
      });
    });
  });

  function userTypeName(code: string): string {
    return USER_TYPES.find((t) => t.code === code)?.name ?? code;
  }

  function toBytes(v: number | string | null | undefined): number {
    if (typeof v === "number") return Number.isFinite(v) ? v : 0;
    if (typeof v === "string") {
      const n = Number(v);
      return Number.isFinite(n) ? n : 0;
    }
    return 0;
  }

  function formatBytes(bytes: number | string | null | undefined): string {
    const n = toBytes(bytes);
    if (!n) return "0 B";
    const k = 1024;
    const units = ["B", "KB", "MB", "GB", "TB"];
    const i = Math.min(Math.floor(Math.log(n) / Math.log(k)), units.length - 1);
    return `${(n / Math.pow(k, i)).toFixed(i === 0 ? 0 : 2)} ${units[i]}`;
  }

  function getDriveSyncStatus(userName: string): DriveSyncAdminStatus | undefined {
    return driveSyncStatusByUser[userName];
  }

  async function loadDriveSyncStatuses() {
    const userIds = list.items.map((u) => u.userName).filter(Boolean);
    if (!userIds.length) return;

    driveSyncStatusLoading = true;
    driveSyncStatusError = null;
    try {
      const client = await getGraphQLClient();
      const res = await client.request<{
        getDriveSyncAdminStatuses: DriveSyncAdminStatus[];
      }>(GetDriveSyncAdminStatusesDocument, {
        userIds,
        includeFolderValidation: true,
        includeUsage: true,
        includeLatestBackup: true,
      });
      const next: Record<string, DriveSyncAdminStatus> = {};
      for (const r of res.getDriveSyncAdminStatuses ?? []) {
        if (r?.userId) next[r.userId] = r;
      }
      driveSyncStatusByUser = next;
      driveSyncStatusLoaded = true;
      toast.success(`DriveSync health checked for ${Object.keys(next).length} user(s).`);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      driveSyncStatusError = msg;
      toast.error(msg);
    } finally {
      driveSyncStatusLoading = false;
    }
  }

  function authHeader(): HeadersInit {
    const token = get(authStore).token;
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  async function refreshDriveSyncOAuthStatus() {
    oauthLoading = true;
    try {
      const res = await fetch(`${getBackendBaseUrl()}/api/drive-sync/oauth/status`, {
        headers: authHeader(),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(body?.error ?? `OAuth status failed (${res.status})`);
      }
      oauthStatus = body as DriveSyncOAuthStatus;
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      oauthLoading = false;
    }
  }

  async function connectDriveSyncOAuth() {
    oauthLoading = true;
    try {
      const res = await fetch(`${getBackendBaseUrl()}/api/drive-sync/oauth/start`, {
        headers: authHeader(),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(body?.error ?? `OAuth start failed (${res.status})`);
      }
      const url = body?.authorizationUrl as string | undefined;
      if (!url) throw new Error("OAuth authorization URL missing from response.");
      window.open(url, "_blank", "noopener,noreferrer,width=640,height=760");
      toast.success("Google OAuth window opened. Complete consent, then click Refresh OAuth.");
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      oauthLoading = false;
    }
  }

  $effect(() => {
    if (list.items.length >= 0) {
      void refreshDriveSyncOAuthStatus();
    }
  });
</script>

<MasterList
  title="User Management"
  description="Manage system access and roles"
  items={list.items}
  totalCount={list.totalCount}
  bind:searchQuery={list.searchQuery.value}
  bind:viewMode
  loading={list.loading}
  loadingMore={list.loadingMore}
  error={list.error}
  hasMore={list.hasMore}
  onLoadMore={list.onLoadMore}
  onRefresh={list.onRefresh}
  sortOptions={[
    { label: "Name (A-Z)", value: "fullName_asc" },
    { label: "Name (Z-A)", value: "fullName_desc" },
    { label: "Username (A-Z)", value: "userName_asc" },
    { label: "Username (Z-A)", value: "userName_desc" },
    { label: "User Type (A-Z)", value: "userType_asc" },
    { label: "User Type (Z-A)", value: "userType_desc" },
    { label: "Status (Active First)", value: "state_asc" },
    { label: "Status (Inactive First)", value: "state_desc" },
  ]}
  currentSort={`${list.pagination.sortField}_${list.pagination.sortDirection}`}
  onSortChange={(val) => {
    const [field, dir] = val.split("_");
    list.pagination.sort(field as any, dir as "asc" | "desc");
  }}
>
  {#snippet filters()}
    <!-- User Type Filter -->
    <div
      class="flex items-center gap-1.5 p-1 bg-muted/30 rounded-lg border border-border/20"
    >
      <Dropdown
        trigger={{
          label: userTypeFilter ? userTypeName(userTypeFilter) : "All",
          variant: "ghost",
          size: "sm",
          showChevron: true,
          class:
            "h-7 px-2.5 text-xs font-medium text-foreground hover:bg-muted/50 data-[state=open]:bg-muted/50",
        }}
        items={[
          { type: "label", label: "User type" },
          { type: "separator" },
          { type: "item", label: "All", onClick: () => (userTypeFilter = "") },
          ...USER_TYPES.map((ut) => ({
            type: "item" as const,
            label: ut.name,
            onClick: () => (userTypeFilter = ut.code),
          })),
        ]}
        align="start"
      />
    </div>

    <div class="w-px h-6 bg-border/80 mx-1 hidden sm:block"></div>

    <!-- Duplicate mobile only -->
    <button
      type="button"
      class={cn(
        "flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium rounded-lg border transition-all",
        showDuplicateMobileOnly
          ? "bg-amber-500/15 text-amber-700 dark:text-amber-400 border-amber-500/40 shadow-sm"
          : "bg-muted/30 border-border/20 text-muted-foreground hover:text-foreground hover:bg-muted/50",
      )}
      onclick={() => (showDuplicateMobileOnly = !showDuplicateMobileOnly)}
      title="Show only users who share a mobile number with another user"
    >
      <Icon name="smartphone" class="size-3.5 shrink-0" />
      <span class="hidden sm:inline">Duplicate mobile</span>
    </button>

    <div class="w-px h-6 bg-border/80 mx-1 hidden sm:block"></div>

    <!-- Status Filters -->
    <div
      class="hidden sm:flex items-center gap-1.5 p-1 bg-muted/30 rounded-lg border border-border/20"
    >
      {#each ["All", "Active", "Inactive"] as status}
        <button
          class={cn(
            "px-2.5 py-1 text-xs font-medium rounded-md transition-all flex items-center gap-1.5",
            statusFilter === status
              ? "bg-background text-primary shadow-sm ring-1 ring-border/50"
              : "text-muted-foreground hover:text-foreground hover:bg-muted/50",
          )}
          onclick={() => (statusFilter = status as any)}
        >
          {#if status !== "All"}
            <span
              class={cn(
                "size-1.5 rounded-full",
                status === "Active" ? "bg-green-500" : "bg-red-500",
              )}
            ></span>
          {/if}
          {status}
        </button>
      {/each}
    </div>
  {/snippet}

  {#snippet actions()}
    <Dropdown
      align="end"
      items={[
        { type: "label", label: "Drive Sync Management" },
        { type: "separator" },
        {
          type: "item",
          label: driveSyncStatusLoading ? "Checking…" : "Check Health",
          icon: "hard-drive",
          disabled:
            driveSyncStatusLoading || list.loading || list.items.length === 0,
          onClick: loadDriveSyncStatuses,
        },
        { type: "separator" },
        {
          type: "item",
          label: "Refresh OAuth",
          icon: "shield-check",
          disabled: oauthLoading,
          onClick: refreshDriveSyncOAuthStatus,
        },
        {
          type: "item",
          label: oauthStatus?.isConfigured
            ? "Reconnect Google"
            : "Connect Google",
          icon: "link",
          disabled: oauthLoading,
          onClick: connectDriveSyncOAuth,
        },
      ]}
    >
      {#snippet children({ props })}
        <Button
          variant="outline"
          size="sm"
          class="gap-2 shrink-0"
          {...props}
        >
          <Icon name="cloud" class="size-3.5" />
          <span class="hidden sm:inline">Drive Sync</span>
          <Icon name="chevron-down" class="size-3.5 opacity-60" />
        </Button>
      {/snippet}
    </Dropdown>
    <Button
      size="sm"
      class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
      onclick={() => goto(`/users/new-user`)}
    >
      <Icon name="plus" class="size-3.5" />
      <span class="hidden sm:inline">Add User</span>
      <span class="sm:hidden">Add</span>
    </Button>
  {/snippet}


  {#snippet gridItem(user: User)}
    <Card
      class="h-full group relative overflow-hidden transition-all duration-300 hover:shadow-md hover:border-primary/50 bg-card/50 backdrop-blur-sm border-border/40 cursor-pointer"
      role="button"
      tabindex={0}
      onclick={() => goto(`/users/${encodeURIComponent(user.userName)}`)}
      onkeydown={(e) =>
        e.key === "Enter" &&
        goto(`/users/${encodeURIComponent(user.userName)}`)}
    >
      <CardContent class="p-3">
        <!-- Header: Avatar + Names + Status -->
        <div class="flex items-start gap-3">
          <!-- Avatar -->
          <div class="relative shrink-0">
            <Avatar
              class="size-10 rounded-lg ring-1 ring-border shadow-sm transition-transform group-hover:scale-105"
            >
              <AvatarImage src="" />
              <AvatarFallback
                class="rounded-lg text-xs font-bold bg-primary/10 text-primary"
              >
                {getInitials(user.fullName, user.userName)}
              </AvatarFallback>
            </Avatar>
            <span
              class="absolute -bottom-1 -right-1 size-3 rounded-full border-2 border-background {user.state ===
              0
                ? 'bg-green-500'
                : 'bg-muted-foreground'}"
            >
              {#if user.state === 0}
                <span
                  class="absolute inset-0 rounded-full bg-green-500 animate-ping opacity-75"
                ></span>
              {/if}
            </span>
          </div>

          <!-- Info -->
          <div class="flex flex-col min-w-0 flex-1">
            <div class="flex items-center justify-between gap-2">
              <h3
                class="font-semibold text-sm truncate text-foreground group-hover:text-primary transition-colors"
              >
                {user.fullName || "Unknown"}
              </h3>
              {#if user.userType}
                <Badge
                  variant="outline"
                  class="text-[10px] px-1.5 py-0 h-4 font-normal bg-muted/50 border-border/50 truncate max-w-[80px]"
                >
                  {userTypeName(user.userType)}
                </Badge>
              {/if}
            </div>

            <p class="text-xs text-muted-foreground truncate font-mono mt-0.5">
              {user.userName || "unknown"}
            </p>

            <!-- Metadata Grid -->
            <div class="mt-3 grid gap-1.5">
              {#if user.authenticationEmail}
                <div
                  class="flex items-center gap-1.5 text-xs text-muted-foreground/80"
                >
                  <Icon name="mail" class="size-3 shrink-0" />
                  <span class="truncate">{user.authenticationEmail}</span>
                </div>
              {/if}
              {#if user.mobileNo}
                <div
                  class="flex items-center gap-1.5 text-xs text-muted-foreground/80"
                >
                  <Icon name="phone" class="size-3 shrink-0" />
                  <span class="truncate">{user.mobileNo}</span>
                </div>
              {/if}
            </div>

            <!-- Additional Info -->
            {#if driveSyncStatusLoaded}
              {@const ds = getDriveSyncStatus(user.userName)}
              <div class="mt-3 pt-2 border-t border-border/30 space-y-1">
                <div class="flex items-center justify-between text-[11px]">
                  <span class="text-muted-foreground">DriveSync</span>
                  {#if ds}
                    {#if ds.isActive && ds.folderValidated !== false}
                      <Badge variant="secondary" class="h-4 px-1.5 text-[10px]">Healthy</Badge>
                    {:else if ds.isActive && ds.folderValidated === false}
                      <Badge variant="destructive" class="h-4 px-1.5 text-[10px]">Folder issue</Badge>
                    {:else}
                      <Badge variant="outline" class="h-4 px-1.5 text-[10px]">Inactive</Badge>
                    {/if}
                  {:else}
                    <span class="text-muted-foreground/70">—</span>
                  {/if}
                </div>
                {#if ds?.isActive}
                  <div class="text-[10px] text-muted-foreground font-mono truncate">
                    {formatBytes(ds.usedBytes)} / {formatBytes(ds.quotaBytes)}
                  </div>
                {/if}
              </div>
            {/if}
          </div>
        </div>
      </CardContent>
    </Card>
  {/snippet}

  {#snippet tableHeader()}
    <TableHead class="w-[80px] text-center">Avatar</TableHead>
    <TableHead
      class="cursor-pointer hover:text-primary transition-colors"
      onclick={() => list.pagination.toggleSort("fullName")}
    >
      <div class="flex items-center gap-2">
        Name
        {#if list.pagination.sortField === "fullName"}
          <Icon
            name={list.pagination.sortDirection === "asc"
              ? "arrow-up"
              : "arrow-down"}
            class="size-3"
          />
        {/if}
      </div>
    </TableHead>
    <TableHead
      class="cursor-pointer hover:text-primary transition-colors hidden md:table-cell"
      onclick={() => list.pagination.toggleSort("userName")}
    >
      <div class="flex items-center gap-2">
        Username
        {#if list.pagination.sortField === "userName"}
          <Icon
            name={list.pagination.sortDirection === "asc"
              ? "arrow-up"
              : "arrow-down"}
            class="size-3"
          />
        {/if}
      </div>
    </TableHead>
    <TableHead
      class="cursor-pointer hover:text-primary transition-colors"
      onclick={() => list.pagination.toggleSort("userType")}
    >
      <div class="flex items-center gap-2">
        User Type
        {#if list.pagination.sortField === "userType"}
          <Icon
            name={list.pagination.sortDirection === "asc"
              ? "arrow-up"
              : "arrow-down"}
            class="size-3"
          />
        {/if}
      </div>
    </TableHead>
    <TableHead class="hidden lg:table-cell">Contact</TableHead>
    <TableHead
      class="cursor-pointer hover:text-primary transition-colors"
      onclick={() => list.pagination.toggleSort("state")}
    >
      <div class="flex items-center gap-2">
        Status
        {#if list.pagination.sortField === "state"}
          <Icon
            name={list.pagination.sortDirection === "asc"
              ? "arrow-up"
              : "arrow-down"}
            class="size-3"
          />
        {/if}
      </div>
    </TableHead>
    <TableHead class="hidden xl:table-cell">DriveSync</TableHead>
    <TableHead class="text-right">Actions</TableHead>
  {/snippet}

  {#snippet tableRow(user: User)}
    <TableCell class="text-center p-2">
      <Avatar
        class="size-10 ring-2 ring-transparent group-hover:ring-primary/20 transition-all"
      >
        <AvatarImage src="" />
        <AvatarFallback class="bg-primary/5 text-primary text-xs font-bold">
          {getInitials(user.fullName, user.userName)}
        </AvatarFallback>
      </Avatar>
    </TableCell>
    <TableCell>
      <div class="font-medium text-foreground">{user.fullName || "N/A"}</div>
      <div class="text-xs text-muted-foreground md:hidden">{user.userName}</div>
    </TableCell>
    <TableCell class="hidden md:table-cell">
      <code
        class="text-xs bg-muted/50 px-1.5 py-0.5 rounded font-mono text-muted-foreground"
      >
        {user.userName || "N/A"}
      </code>
    </TableCell>
    <TableCell>
      <div class="flex items-center gap-2">
        <Icon name="shield" class="size-3.5 text-muted-foreground" />
        <span class="text-sm"
          >{user.userType ? userTypeName(user.userType) : "None"}</span
        >
      </div>
    </TableCell>
    <TableCell class="hidden lg:table-cell">
      <div class="flex flex-col gap-1 text-xs">
        {#if user.mobileNo?.trim()}
          <div class="flex items-center gap-2 text-muted-foreground">
            <Icon name="phone" class="size-3 shrink-0" />
            <span class="truncate max-w-[140px]">{user.mobileNo}</span>
          </div>
        {/if}
        {#if user.authenticationEmail?.trim()}
          <div class="flex items-center gap-2 text-muted-foreground">
            <Icon name="mail" class="size-3 shrink-0" />
            <span class="truncate max-w-[140px]">{user.authenticationEmail}</span>
          </div>
        {/if}
        {#if !user.mobileNo?.trim() && !user.authenticationEmail?.trim()}
          <span class="text-muted-foreground/60">—</span>
        {/if}
      </div>
    </TableCell>
    <TableCell>
      <StatusBadge
        status={user.state}
        context="ACTIVE_DISABLED"
        class="text-xs font-normal"
      />
    </TableCell>
    <TableCell class="hidden xl:table-cell">
      {#if driveSyncStatusLoaded}
        {@const ds = getDriveSyncStatus(user.userName)}
        {#if ds}
          <div class="flex flex-col gap-1 text-xs">
            <div class="flex items-center gap-2">
              {#if ds.isActive && ds.folderValidated !== false}
                <span class="size-1.5 rounded-full bg-emerald-500"></span>
                <span>Healthy</span>
              {:else if ds.isActive && ds.folderValidated === false}
                <span class="size-1.5 rounded-full bg-amber-500"></span>
                <span>Folder issue</span>
              {:else}
                <span class="size-1.5 rounded-full bg-muted-foreground"></span>
                <span>Inactive</span>
              {/if}
            </div>
            {#if ds.isActive}
              <div class="text-muted-foreground font-mono">
                {formatBytes(ds.usedBytes)} / {formatBytes(ds.quotaBytes)}
              </div>
            {/if}
            {#if ds.folderValidationError}
              <div class="text-amber-600 dark:text-amber-500 truncate max-w-[220px]" title={ds.folderValidationError}>
                {ds.folderValidationError}
              </div>
            {/if}
          </div>
        {:else}
          <span class="text-muted-foreground/60">—</span>
        {/if}
      {:else}
        <span class="text-muted-foreground/60">Run check</span>
      {/if}
    </TableCell>
    <TableCell class="text-right">
      <TableActions
        title={user.fullName}
        actions={[
          {
            label: "View Details",
            icon: "eye",
            onClick: () => goto(`/users/${encodeURIComponent(user.userName)}`),
          },
          {
            label: "Edit User",
            icon: "pencil",
            onClick: () => goto(`/users/${encodeURIComponent(user.userName)}`),
          },
        ]}
      />
    </TableCell>
  {/snippet}
</MasterList>
