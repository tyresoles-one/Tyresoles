<script lang="ts">
  import { usePaginatedList } from "$lib/composables";
  import { TableActions } from "$lib/components/venUI/tableActions";
  import { StatusBadge } from "$lib/components/venUI/statusBadge";
  import { Card, CardContent } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { Avatar, AvatarFallback, AvatarImage } from "$lib/components/ui/avatar";
  import { Icon } from "$lib/components/venUI/icon";
  import { TableCell, TableHead } from "$lib/components/ui/table";
  import MasterList from "$lib/components/venUI/masterList/MasterList.svelte";
  import { cn, getInitials } from "$lib/utils";
  import { toast } from "svelte-sonner";
  import { gql, request } from "graphql-request";
  
  import * as Sheet from "$lib/components/ui/sheet";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";

  // 1. Fetch Users Query
  const GetUsersDocument: any = gql`
    query GetUsers($skip: Int, $take: Int, $where: UserFilterInput, $order: [UserSortInput!]) {
      users(skip: $skip, take: $take, where: $where, order: $order) {
        items {
          userId
          fullName
          userName
          userType
          state
        }
        totalCount
      }
    }
  `;

  // 2. Fetch User Drive Config Query
  const GetConfigDocument = gql`
    query GetDriveSyncConfig($userId: String!) {
      driveSyncConfig(targetUserId: $userId) {
        id
        targetFolderId
        quotaBytes
        usedBytes
        allowedExtensionsJson
        isActive
      }
    }
  `;

  // 3. Save Mutation
  const SaveConfigDocument = gql`
    mutation SaveDriveSyncConfig($input: DriveSyncUserConfigInput!) {
      saveDriveSyncConfig(input: $input) {
        id
        targetFolderId
        quotaBytes
      }
    }
  `;

  type User = {
    userId: string;
    fullName: string;
    userName: string;
    userType: string;
    state: number;
  };

  type DriveSyncConfig = {
    targetFolderId: string;
    quotaBytes: number;
    usedBytes: number;
    allowedExtensionsJson: string | null;
    isActive: boolean;
  };

  let viewMode = $state<"grid" | "table">("table");

  // Master List Controller
  const list = usePaginatedList<User>({
    query: GetUsersDocument,
    dataPath: "users",
    pageSize: 50,
  });

  // Sheet State
  let sheetOpen = $state(false);
  let selectedUser = $state<User | null>(null);
  let configLoading = $state(false);
  let isSaving = $state(false);
  
  // Form State
  let config = $state<Partial<DriveSyncConfig>>({
    targetFolderId: "",
    quotaBytes: 0,
    isActive: true,
    allowedExtensionsJson: null
  });

  async function openConfigSheet(user: User) {
    selectedUser = user;
    sheetOpen = true;
    configLoading = true;
    
    try {
      // NOTE: Adjust the endpoint logic to match your real GraphQL client wrapper logic
      const data: any = await request('/graphql', GetConfigDocument, { userId: user.userId });
      if (data.driveSyncConfig) {
        config = { ...data.driveSyncConfig };
      } else {
        // Reset defaults if new
        config = { targetFolderId: "", quotaBytes: 10737418240, isActive: true, allowedExtensionsJson: null }; // 10GB default
      }
    } catch (e: any) {
      toast.error("Failed to load user configuration", { description: e.message });
    } finally {
      configLoading = false;
    }
  }

  async function saveConfig() {
    if (!selectedUser) return;
    isSaving = true;
    try {
      await request('/graphql', SaveConfigDocument, {
        input: {
          userId: selectedUser.userId,
          targetFolderId: config.targetFolderId,
          quotaBytes: config.quotaBytes,
          isActive: config.isActive,
          allowedExtensionsJson: config.allowedExtensionsJson
        }
      });
      toast.success("Drive Sync Configuration saved successfully.");
      sheetOpen = false;
    } catch (e: any) {
      toast.error("Failed to save config", { description: e.message });
    } finally {
      isSaving = false;
    }
  }

  function formatBytes(bytes: number) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
</script>

<MasterList
  title="Drive Sync Management"
  description="Manage per-user Google Drive folder mapping and storage quotas."
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
    { label: "Name (A-Z)", value: "fullName_asc" }
  ]}
  currentSort={`${list.pagination.sortField}_${list.pagination.sortDirection}`}
  onSortChange={(val) => {
    const [field, dir] = val.split("_");
    list.pagination.sort(field as any, dir as "asc" | "desc");
  }}
>
  {#snippet gridItem(user)}
    <Card
      class="h-full group relative overflow-hidden transition-all duration-300 hover:shadow-md hover:border-primary/50 cursor-pointer"
      onclick={() => openConfigSheet(user)}
    >
      <CardContent class="p-3">
        <div class="flex items-start gap-3">
          <Avatar class="size-10 rounded-lg ring-1 ring-border shadow-sm">
            <AvatarFallback class="rounded-lg text-xs font-bold bg-primary/10 text-primary">
              {getInitials(user.fullName, user.userName)}
            </AvatarFallback>
          </Avatar>
          <div class="flex flex-col min-w-0 flex-1">
            <h3 class="font-semibold text-sm truncate">{user.fullName || "Unknown"}</h3>
            <p class="text-xs text-muted-foreground font-mono">{user.userName}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  {/snippet}

  {#snippet tableHeader()}
    <TableHead class="w-[80px] text-center">Avatar</TableHead>
    <TableHead>User</TableHead>
    <TableHead>Username</TableHead>
    <TableHead>User Type</TableHead>
    <TableHead>Status</TableHead>
    <TableHead class="text-right">Actions</TableHead>
  {/snippet}

  {#snippet tableRow(user)}
    <TableCell class="text-center p-2">
      <Avatar class="size-10 ring-2 ring-transparent transition-all">
        <AvatarFallback class="bg-primary/5 text-primary text-xs font-bold">
          {getInitials(user.fullName, user.userName)}
        </AvatarFallback>
      </Avatar>
    </TableCell>
    <TableCell>
      <div class="font-medium">{user.fullName || "N/A"}</div>
    </TableCell>
    <TableCell>
      <code class="text-xs bg-muted/50 px-1.5 py-0.5 rounded font-mono text-muted-foreground">
        {user.userName || "N/A"}
      </code>
    </TableCell>
    <TableCell>
      <div class="flex items-center gap-2">
        <Icon name="shield" class="size-3.5 text-muted-foreground" />
        <span class="text-sm">{user.userType || "None"}</span>
      </div>
    </TableCell>
    <TableCell>
      <StatusBadge status={user.state} context="ACTIVE_DISABLED" class="text-xs font-normal" />
    </TableCell>
    <TableCell class="text-right">
       <Button variant="outline" size="sm" onclick={() => openConfigSheet(user)}>
         Configure Sync
       </Button>
    </TableCell>
  {/snippet}
</MasterList>

<!-- Slide-out Sheet for Configuration -->
<Sheet.Root bind:open={sheetOpen}>
  <Sheet.Content class="sm:max-w-[450px]">
    <Sheet.Header>
      <Sheet.Title>Drive Sync Configuration</Sheet.Title>
      <Sheet.Description>
        Allocate a target Google Drive Folder and storage quota for {selectedUser?.fullName}.
      </Sheet.Description>
    </Sheet.Header>
    
    {#if configLoading}
      <div class="flex items-center justify-center p-12">
         <Icon name="loader-2" class="size-8 animate-spin text-primary" />
      </div>
    {:else}
      <div class="grid gap-6 py-6">
        <div class="grid gap-2">
          <Label for="folderId">Google Drive Folder ID</Label>
           <Input 
             id="folderId" 
             placeholder="e.g. 1aB2c3D4e5F6g7H8i9J0kL" 
             bind:value={config.targetFolderId} 
           />
           <p class="text-[10px] text-muted-foreground leading-tight mt-1">This ID is extracted from the Google Drive URL when you open the destination folder.</p>
        </div>
        
        <div class="grid gap-2">
          <Label for="quota">Quota (in Bytes)</Label>
           <Input 
             id="quota" 
             type="number" 
             bind:value={config.quotaBytes} 
           />
           <p class="text-xs text-muted-foreground font-medium text-emerald-600 dark:text-emerald-400">
             {formatBytes(Number(config.quotaBytes) || 0)} allocated
           </p>
        </div>
        
        {#if config.usedBytes !== undefined}
          <div class="p-3 bg-muted/50 rounded-lg border border-border/50 grid gap-1.5">
             <div class="text-xs font-medium text-muted-foreground uppercase flex items-center justify-between">
               <span>Currently Used</span>
               <span class="text-foreground">{formatBytes(config.usedBytes)}</span>
             </div>
             
             <!-- Simple Progress Bar -->
             <div class="h-2 w-full bg-border rounded-full overflow-hidden">
               <div class="h-full bg-primary" style="width: {Math.min(100, (config.usedBytes / (config.quotaBytes || 1)) * 100)}%"></div>
             </div>
          </div>
        {/if}
      </div>
      
      <Sheet.Footer>
        <Button variant="outline" onclick={() => sheetOpen = false}>Cancel</Button>
        <Button disabled={isSaving} onclick={saveConfig}>
           {#if isSaving}
              <Icon name="loader-2" class="size-4 animate-spin mr-2" /> Saving...
           {:else}
              Save Configuration
           {/if}
        </Button>
      </Sheet.Footer>
    {/if}
  </Sheet.Content>
</Sheet.Root>
