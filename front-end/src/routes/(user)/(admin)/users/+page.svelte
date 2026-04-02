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

  // GraphQL & utils
  import { gql } from "graphql-request";
  const GetUsersDocument: any = gql`
    query GetUsers($skip: Int, $take: Int, $where: String, $order: String) {
      user(username: "dummy") {
        userId
      }
    }
  `;
/*
  const GetUsersDocument: any = gql`
    query GetUsers($skip: Int, $take: Int, $where: UserFilterInput, $order: [UserSortInput!]) {
      users(skip: $skip, take: $take, where: $where, order: $order) {
        items {
          userId
          fullName
          userType
          mobileNo
          emails
          state
          avatar
        }
        totalCount
      }
    }
  `;
*/

  type GetUsersQuery = {
    users: {
      items: Array<{
        userName: string;
        fullName: string;
        userType: string;
        mobileNo?: string;
        emails?: string;
        state: number;
        avatar?: string;
      }>;
      totalCount: number;
    };
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

  const list = usePaginatedList<User>({
    query: GetUsersDocument,
    dataPath: "users",
    pageSize: 50,
  });

  let lastState = $state<number | null | "init">("init");
  let lastUserType = $state<string | undefined | "init">("init");
  let lastDuplicateMobileOnly = $state<true | undefined | "init">("init");
  $effect(() => {
    const state =
      statusFilter === "Active" ? 0 : statusFilter === "Inactive" ? 1 : null;
    const userType = userTypeFilter || undefined;
    const duplicateMobileOnly = showDuplicateMobileOnly ? true : undefined;
    const prevDup =
      lastDuplicateMobileOnly === "init" ? undefined : lastDuplicateMobileOnly;
    if (
      state === lastState &&
      userType === lastUserType &&
      duplicateMobileOnly === prevDup
    )
      return;
    lastState = state;
    lastUserType = userType;
    lastDuplicateMobileOnly = duplicateMobileOnly;
    list.pagination.setVariables({ state, userType, duplicateMobileOnly });
  });

  function userTypeName(code: string): string {
    return USER_TYPES.find((t) => t.code === code)?.name ?? code;
  }
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
    <Button
      size="sm"
      class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
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
              {#if user.emails}
                <div
                  class="flex items-center gap-1.5 text-xs text-muted-foreground/80"
                >
                  <Icon name="mail" class="size-3 shrink-0" />
                  <span class="truncate">{user.emails}</span>
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
        {#if user.emails?.trim()}
          <div class="flex items-center gap-2 text-muted-foreground">
            <Icon name="mail" class="size-3 shrink-0" />
            <span class="truncate max-w-[140px]">{user.emails}</span>
          </div>
        {/if}
        {#if !user.mobileNo?.trim() && !user.emails?.trim()}
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
