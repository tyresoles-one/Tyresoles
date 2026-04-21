<script lang="ts">
	import { untrack } from 'svelte';
	import { page } from '$app/stores';
	import { browser } from '$app/environment';
	import { goto } from '$app/navigation';
	import { fade, slide } from 'svelte/transition';
	import { flip } from 'svelte/animate';
	import { Dialog } from '$lib/components/venUI/dialog';
	
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '$lib/components/ui/card';
	import { Avatar, AvatarFallback, AvatarImage } from '$lib/components/ui/avatar';
	import { Badge } from '$lib/components/ui/badge';
	import { StatusBadge } from '$lib/components/venUI/statusBadge';
	import { Checkbox } from '$lib/components/ui/checkbox';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Separator } from '$lib/components/ui/separator';
	import * as Select from '$lib/components/ui/select';
	import { Select as VenSelect } from '$lib/components/venUI/select';
	import * as Tabs from '$lib/components/ui/tabs';
	import { Icon } from '$lib/components/venUI/icon';
	import { DatePicker } from '$lib/components/venUI/date-picker';
    import { toast } from '$lib/components/venUI/toast';

    import { useAppQuery, useAppMutation } from '$lib/services/graphql/queryClient';
	import { getGraphQLClient } from '$lib/services/graphql/client';
	import gql from 'graphql-tag';

	import { GetUserDocument, type GetUserQuery } from '$lib/services/graphql/generated/types';

	// Admin list/mutation operations are not yet in schema/codegen; stubs keep the bundle buildable (see respCenters/+page.svelte).
	const GetResponsibilityCentersDocument = gql`
		query GetResponsibilityCenters($skip: Int, $take: Int, $where: ResponsibilityCenterFilterInput) {
			responsibilityCenters(skip: $skip, take: $take, where: $where) {
				items {
					code
					name
				}
				totalCount
			}
		}
	` as any;
	type GetResponsibilityCentersQuery = any;

	const GetEmployeesDocument = gql`
		query GetEmployees($skip: Int, $take: Int, $where: EmployeeFilterInput) {
			employees(skip: $skip, take: $take, where: $where) {
				items {
					no
					firstName
					lastName
				}
				totalCount
			}
		}
	` as any;
	type GetEmployeesQuery = any;

	const GetPermissionSetsDocument = gql`
		query GetPermissionSets($take: Int) {
			permissionSets(take: $take) {
				items {
					roleID
					name
				}
				totalCount
			}
		}
	` as any;
	type GetPermissionSetsQuery = any;

	const UpdateUserPermissionsDocument = gql`
		mutation UpdateUserPermissions($userName: String!, $permissions: [UserPermissionInput!]!) {
			updateUserPermissions(userName: $userName, permissions: $permissions) {
				success
				message
			}
		}
	` as any;
	type UpdateUserPermissionsMutation = any;

	const UpdateUserResponsibilityCentersDocument = gql`
		mutation UpdateUserResponsibilityCenters($userName: String!, $assignments: [UserRespCenterInput!]!) {
			updateUserResponsibilityCenters(userName: $userName, assignments: $assignments) {
				success
				message
			}
		}
	` as any;
	type UpdateUserResponsibilityCentersMutation = any;

	const UpdateUserPostingSetupDocument = gql`
		mutation UpdateUserPostingSetup($userName: String!, $assignments: [UserPostingSetupInput!]!) {
			updateUserPostingSetup(userName: $userName, assignments: $assignments) {
				success
				message
			}
		}
	` as any;
	type UpdateUserPostingSetupMutation = any;

	const UpdateUserDetailsDocument = gql`
		mutation UpdateUserDetails($userName: String!, $details: ProfileUpdateInput!) {
			updateUserDetails(userName: $userName, details: $details) {
				success
				message
			}
		}
	` as any;
	type UpdateUserDetailsMutation = any;

	const GetReportsDocument = gql`
		query GetReports($category: String!) {
			reports: reportsByCategory(category: $category) {
				id
				name
			}
		}
	` as any;

	type RespCenterSetupEntry = {
		userId: string;
		respCenter: string;
		default: number;
		type: number;
		code: string;
	};

	type PostingSetupEntry = {
		userId: string;
		responsibilityCenter: string;
		allowPostingFrom: string;
		allowPostingTo: string;
	};

	const GetUserDetailDocument = gql`
		query GetUserDetail($username: String!) {
			user(username: $username) {
				userId
				fullName
				userType
				mobileNo
				authenticationEmail
				state
				canRunERP
				canRunOldERP
				allowAllMasters
				backupStorageQuotaGB
				backupAllowedFileTypes
				backupGDriveFolderID
				respCenterSetup {
					userId
					respCenter
					respDefault
					type
					code
				}
				postingSetup {
					responsibilityCenter
					allowPostingFrom
					allowPostingTo
				}
				permissions {
					roleId
					roleName
					companyName
					assignerName
					roleExipryDate
					values
					homePath
				}
			}
		}
	`;

	const REPORT_ROLES: Record<string, string> = {
		'RPT-ACCS': 'accounts',
		'RPT-PAYRL': 'payroll',
		'RPT-PROD': 'production',
		'RPT-SALES': 'sales'
	};

	// Route param is userName
	let userId = $derived(decodeURIComponent($page.params.userId ?? '').trim());
	let isNewUser = $derived(userId === 'new-user');
	
    // Queries powered by Tanstack for caching, deduping, and auto-loading states
	const userQuery = useAppQuery<any, any>(
		GetUserDetailDocument,
		() => ({ username: userId }),
		() => ({ enabled: !!userId && !isNewUser })
	);
	const rcQuery = useAppQuery<GetResponsibilityCentersQuery, any>(GetResponsibilityCentersDocument, () => ({ natureOfBusiness: [0, 1, 2, 3, 4], skip: 0, take: 500 }));
	const empQuery = useAppQuery<GetEmployeesQuery, any>(GetEmployeesDocument, () => ({ skip: 0, take: 500 }));
	const permQuery = useAppQuery<GetPermissionSetsQuery, any>(GetPermissionSetsDocument, () => ({ take: 1000 }));

    // Mutations powered by Tanstack for global toast handling and pending states
    const permsMut = useAppMutation<UpdateUserPermissionsMutation, any>(UpdateUserPermissionsDocument, { silent: true });
    const rcMut = useAppMutation<UpdateUserResponsibilityCentersMutation, any>(UpdateUserResponsibilityCentersDocument, { silent: true });
    const postingMut = useAppMutation<UpdateUserPostingSetupMutation, any>(UpdateUserPostingSetupDocument, { silent: true });
    const userMut = useAppMutation<UpdateUserDetailsMutation, any>(UpdateUserDetailsDocument, { silent: true });

	let respCenterOptions = $derived(
        rcQuery.data?.responsibilityCenters?.items?.map((i: any) => ({ code: i.code, name: i.name })) ?? []
    );
	let employeeOptions = $derived(
        empQuery.data?.employees?.items?.map((i: any) => {
            const n = [i.firstName, i.lastName].filter(Boolean).join(' ').trim();
            const label = n ? `${n} (${i.no})` : i.no;
            return { no: i.no, firstName: i.firstName, lastName: i.lastName, label };
        }) ?? []
    );
	let permissionOptions = $derived(
        permQuery.data?.permissionSets?.items?.map((i: any) => ({ roleId: i.roleID, name: i.name || i.roleID })) ?? []
    );

    // Draft state for 2-way binding to form inputs
	let user = $state({
		fullName: '',
		userName: '',
		email: '',
		mobile: '',
		userType: '',
		status: 0,
		lastActive: new Date().toISOString(),
		canRunErp: 0,
		canRunOldErp: 0,
		allowAllMasters: 0,
		backupStorageQuotaGb: 0,
		backupAllowedFileTypes: '',
		backupGDriveFolderId: '',
		respCenterSetup: [] as RespCenterSetupEntry[],
		postingSetup: [] as PostingSetupEntry[],
		permissions: [] as { 
			roleId: string; 
			roleName: string; 
			companyName: string; 
			assignerName: string; 
			roleExipryDate: string; 
			values: string;
			_values: (string | number)[]; 
			homePath: number;
		}[]
	});

	let loading = $derived(userQuery.isPending && !isNewUser);
	let saving = $derived(permsMut.isPending || rcMut.isPending || postingMut.isPending || userMut.isPending);
	let error = $derived(
		!isNewUser && userQuery.isError
			? userQuery.error.message
			: !isNewUser && userQuery.isSuccess && !userQuery.data?.user
				? 'User not found'
				: undefined
	);

    // Sync remote data into local state when the query result updates (avoid reading `user` here so typing in the form does not retrigger and reset).
    $effect(() => {
        const foundUser = userQuery.data?.user;
        if (!foundUser || !userId || isNewUser) return;

        untrack(() => {
            const from = (s: string | null | undefined) => (s ? String(s).slice(0, 10) : '');
            const permRows = (foundUser.permissions ?? []).map((p: any) => {
                const roleId = p.roleId ?? '';
                const values = p.values ?? '';
                let _vals: (string | number)[] = [];
                if (REPORT_ROLES[roleId] && values) {
                    _vals = values
                        .split(',')
                        .map((x: string) => x.trim())
                        .filter(Boolean)
                        .map((x: string) => {
                            const n = Number(x);
                            return Number.isFinite(n) ? n : x;
                        });
                }
                return {
                    roleId,
                    roleName: p.roleName ?? '',
                    companyName: p.companyName ?? '',
                    assignerName: p.assignerName ?? '',
                    roleExipryDate: p.roleExipryDate ?? '',
                    values,
                    _values: _vals,
                    homePath: p.homePath ?? 0
                };
            });

            user = {
                ...user,
                fullName: foundUser.fullName ?? '',
                userName: userId,
                userType: foundUser.userType ?? '',
                email: foundUser.authenticationEmail ?? '',
                mobile: foundUser.mobileNo ?? '',
                status: foundUser.state ?? 0,
                canRunErp: foundUser.canRunERP ?? 0,
                canRunOldErp: foundUser.canRunOldERP ?? 0,
                allowAllMasters: foundUser.allowAllMasters ?? 0,
                backupStorageQuotaGb: foundUser.backupStorageQuotaGB ?? 0,
                backupAllowedFileTypes: foundUser.backupAllowedFileTypes ?? '',
                backupGDriveFolderId: foundUser.backupGDriveFolderID ?? '',
                lastActive: new Date().toISOString(),
                respCenterSetup: (foundUser.respCenterSetup ?? []).map((r: any) => ({
                    userId: userId,
                    respCenter: r.respCenter ?? '',
                    default: r.respDefault ?? 0,
                    type: r.type ?? 0,
                    code: r.code ?? ''
                })),
                postingSetup: (foundUser.postingSetup ?? []).map((p: any) => ({
                    userId: userId,
                    responsibilityCenter: p.responsibilityCenter ?? '',
                    allowPostingFrom: from(p.allowPostingFrom),
                    allowPostingTo: from(p.allowPostingTo)
                })),
                permissions: permRows
            };
        });
    });

    let reportsCache = $state<Record<string, { id: number; name: string }[]>>({});

    async function loadReportsIfNeeded(roleId: string) {
        const category = REPORT_ROLES[roleId];
        if (!category || reportsCache[category]?.length > 0) return;
        try {
            // Native graphql request since this is an imperative call inside a loop
            const client = await getGraphQLClient();
            const res = await client.request<any, any>(GetReportsDocument as any, { category });
            if (res.reports) reportsCache[category] = res.reports;
        } catch (e) {
            console.error(`Failed to load reports for ${category}`, e);
        }
    }

	let initials = $derived(
		user.fullName ? user.fullName.split(' ').map((n) => n[0]).join('').toUpperCase().slice(0, 2) : 'U'
	);

	function addRespCenter() {
		user.respCenterSetup = [...user.respCenterSetup, { userId: user.userName, respCenter: '', default: 0, type: 0, code: '' }];
	}
	function removeRespCenter(index: number) {
		user.respCenterSetup = user.respCenterSetup.filter((_, i) => i !== index);
	}
	function addPermission() {
		user.permissions = [...user.permissions, { roleId: '', roleName: '', companyName: '', assignerName: '', roleExipryDate: '', values: '', _values: [], homePath: 0 }];
	}
	function removePermission(index: number) {
		user.permissions = user.permissions.filter((_, i) => i !== index);
	}
	function addPostingSetup() {
		user.postingSetup = [...user.postingSetup, { userId: user.userName, responsibilityCenter: '', allowPostingFrom: '', allowPostingTo: '' }];
	}
	function removePostingSetup(index: number) {
		user.postingSetup = user.postingSetup.filter((_, i) => i !== index);
	}
	function toIsoDate(s: string): string {
		if (!s) return new Date().toISOString().slice(0, 10) + 'T00:00:00.000Z';
		if (s.includes('T')) return s;
		return s + 'T00:00:00.000Z';
	}

	let respCenterSelectOptions = $derived([{ code: '', name: 'Select...' }, ...respCenterOptions]);
	let employeeSelectOptions = $derived([{ no: '', label: 'Select...', firstName: '', lastName: '' }, ...employeeOptions]);
	let permissionSelectOptions = $derived([{ roleId: '', name: 'Select Permission...' }, ...permissionOptions.map((p: any) => ({ roleId: p.roleId, name: `${p.roleId} - ${p.name}` }))]);

	const TYPE_OPTIONS = [
		{ value: 0, label: 'Select...' },
		{ value: 1, label: 'Employee' },
		{ value: 2, label: 'Customer' },
		{ value: 3, label: 'Partner' }
	] as const;

	async function saveUser() {
		const confirmed = await Dialog.confirm(
			'Save Changes',
			'Are you sure you want to save the changes made to the user?',
			{ confirmLabel: 'Save', cancelLabel: 'Cancel' }
		);
		if (!confirmed) return;

		try {
            await Promise.all([
                permsMut.mutateAsync({
                    userName: user.userName,
                    permissions: user.permissions.filter(p => p.roleId).map(p => ({
                        roleId: p.roleId,
                        values: p.values || '',
                        homePath: p.homePath || 0
                    }))
                }),
                rcMut.mutateAsync({
                    userName: user.userName,
                    assignments: user.respCenterSetup.filter(r => r.respCenter).map(r => ({
                        respCenter: r.respCenter,
                        default: r.default || 0,
                        type: r.type || 0,
                        code: r.code || ''
                    }))
                }),
                postingMut.mutateAsync({
                    userName: user.userName,
                    assignments: user.postingSetup.filter((p) => p.responsibilityCenter).map((p) => ({
                        responsibilityCenter: p.responsibilityCenter,
                        allowPostingFrom: toIsoDate(p.allowPostingFrom),
                        allowPostingTo: toIsoDate(p.allowPostingTo)
                    }))
                }),
                userMut.mutateAsync({
                    userName: user.userName,
                    details: {
                        fullName: user.fullName || '',
                        userType: user.userType || '',
                        mobileNo: user.mobile || '',
                        authenticationEmail: user.email || '',
                        state: user.status,
                        canRunErp: user.canRunErp ? 1 : 0,
                        canRunOldErp: user.canRunOldErp ? 1 : 0,
                        allowAllMasters: user.allowAllMasters ? 1 : 0,
                        backupStorageQuotaGB: user.backupStorageQuotaGb,
                        backupAllowedFileTypes: user.backupAllowedFileTypes,
                        backupGDriveFolderID: user.backupGDriveFolderId
                    }
                })
            ]);
            
            // Refetch fresh data to guarantee UI parity
            userQuery.refetch();
			toast.success('User updated successfully');
		} catch (e) {
            // Error toasts are handled by queryClient globally, but if something fails here, we can fallback
            console.error("Save failure bubble:", e);
		}
	}
</script>

<div class="min-h-screen bg-background relative pb-20">
  <!-- Ambient Background -->
  <div class="fixed inset-0 -z-10 pointer-events-none overflow-hidden">
    <div
      class="absolute top-[10%] right-[10%] h-[500px] w-[500px] rounded-full bg-primary/5 blur-[100px]"
    ></div>
    <div
      class="absolute bottom-[10%] left-[10%] h-[400px] w-[400px] rounded-full bg-blue-500/5 blur-[90px]"
    ></div>
  </div>

  <!-- Header -->
  <header
    class="sticky top-0 z-40 w-full border-b bg-background/80 backdrop-blur-xl"
  >
    <div class="container mx-auto px-4 py-4 flex items-center justify-between">
      <div class="flex items-center gap-4">
        <Button
          variant="ghost"
          size="icon"
          onclick={() => history.back()}
          class="shrink-0"
        >
          <Icon name="arrow-left" class="size-5" />
        </Button>
        <div>
          <h1 class="text-xl font-bold tracking-tight">
            {#if loading}
              <Skeleton class="h-8 w-48" />
            {:else if isNewUser}
              Add User
            {:else}
              Edit User
            {/if}
          </h1>
          <p class="text-sm text-muted-foreground hidden sm:block">
            Update user information and permissions
          </p>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <Button variant="outline" onclick={() => history.back()}>Cancel</Button>
        <Button
          onclick={saveUser}
          disabled={loading || saving}
          class="min-w-[100px]"
        >
          {#if saving}
            <Icon name="loader-2" class="size-4 animate-spin mr-2" />
            Saving...
          {:else}
            <Icon name="save" class="size-4 mr-2" />
            Save
          {/if}
        </Button>
      </div>
    </div>
  </header>

  <main class="container mx-auto px-4 py-6 max-w-4xl">
    {#if loading}
      <div class="space-y-6">
        <Skeleton class="h-[120px] w-full rounded-xl" />
        <Skeleton class="h-[400px] w-full rounded-xl" />
      </div>
    {:else if error}
      <div
        class="flex flex-col items-center justify-center py-16 text-center"
        in:fade={{ duration: 200 }}
      >
        <Icon name="user-x" class="size-12 text-muted-foreground mb-4" />
        <h2 class="text-lg font-semibold text-foreground mb-2">{error}</h2>
        <p class="text-sm text-muted-foreground mb-4">
          The user may not exist or you may not have access.
        </p>
        <Button onclick={() => goto("/users")}>Back to users</Button>
      </div>
    {:else}
      <div
        class="grid grid-cols-1 md:grid-cols-12 gap-6"
        in:fade={{ duration: 300 }}
      >
        <!-- Left Column: Compact Profile -->
        <div class="md:col-span-4 space-y-4">
          <Card class="overflow-hidden border-border/40 shadow-none bg-card/40">
            <CardContent
              class="pt-6 pb-6 flex flex-col items-center text-center"
            >
              <Avatar class="size-20 ring-2 ring-border mb-3">
                <AvatarImage src="" />
                <AvatarFallback
                  class="text-xl font-medium bg-primary/5 text-primary"
                >
                  {initials}
                </AvatarFallback>
              </Avatar>
              <h2 class="text-lg font-semibold">{user.fullName}</h2>
              <p class="text-sm text-muted-foreground mb-3">@{user.userName}</p>

              <StatusBadge
                status={user.status}
                context="ACTIVE_DISABLED"
                class="px-2 py-0.5 text-xs font-normal"
              />

              <div class="w-full mt-6 space-y-3 text-sm">
                <div
                  class="flex justify-between py-2 border-b border-border/40"
                >
                  <span class="text-muted-foreground">User Type</span>
                  <span class="font-medium">{user.userType}</span>
                </div>
                <!-- Dept removed -->
                <div class="flex justify-between pt-2">
                  <span class="text-muted-foreground">Last Active</span>
                  <span class="font-medium"
                    >{new Date(user.lastActive).toLocaleDateString()}</span
                  >
                </div>
              </div>

              {#if user.respCenterSetup.length > 0}
                <div
                  class="w-full mt-6 pt-4 border-t border-border/40 text-left"
                >
                  <p
                    class="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2"
                  >
                    Responsibility Centers
                  </p>
                  <div class="flex flex-wrap gap-1.5">
                    {#each user.respCenterSetup as rc}
                      <Badge
                        variant="secondary"
                        class="font-mono text-[10px] px-1.5"
                        title={rc.code
                          ? `${rc.respCenter} • ${rc.code}${rc.default === 1 ? " (default)" : ""}`
                          : undefined}
                      >
                        {rc.respCenter}{#if rc.default === 1}<span
                            class="opacity-70"
                          >
                            ★</span
                          >{/if}
                      </Badge>
                    {/each}
                  </div>
                </div>
              {/if}
            </CardContent>
          </Card>

          <!-- Simple Actions -->
          <div class="space-y-2">
            <Button variant="outline" class="w-full justify-start h-9 text-sm">
              <Icon name="key" class="size-3.5 mr-2" />
              Reset Password
            </Button>
            <Button
              variant="outline"
              class="w-full justify-start h-9 text-sm {user.status === 1
                ? 'text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50'
                : 'text-destructive hover:text-destructive hover:bg-destructive/10'}"
              onclick={() => {
                // Toggle status: 0 (Enabled) <-> 1 (Disabled)
                user.status = user.status === 1 ? 0 : 1;
              }}
            >
              <Icon
                name={user.status === 1 ? "circle-check" : "ban"}
                class="size-3.5 mr-2"
              />
              {user.status === 1 ? "Enable Account" : "Disable Account"}
            </Button>
          </div>
        </div>

        <!-- Right Column: Settings -->
        <div class="md:col-span-8">
          <Tabs.Root value="general" class="w-full">
            <Tabs.List
              class="w-full justify-start border-b h-auto p-0 mb-4 bg-transparent"
            >
              <Tabs.Trigger
                value="general"
                class="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:text-primary px-4 py-2 text-sm"
                >General</Tabs.Trigger
              >
              <Tabs.Trigger
                value="resp-setup"
                class="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:text-primary px-4 py-2 text-sm"
                >Resp Setup</Tabs.Trigger
              >
              <Tabs.Trigger
                value="posting-setup"
                class="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:text-primary px-4 py-2 text-sm"
                >Posting Setup</Tabs.Trigger
              >
              <Tabs.Trigger
                value="permissions"
                class="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:text-primary px-4 py-2 text-sm"
                >Permissions</Tabs.Trigger
              >
            </Tabs.List>

            <Tabs.Content value="general">
              <div in:slide={{ axis: "y", duration: 300 }}>
                <Card class="border-border/40 shadow-none bg-card/40">
                  <CardHeader class="pb-4">
                    <CardTitle class="text-base">Details</CardTitle>
                  </CardHeader>
                  <CardContent class="space-y-4">
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      <div class="space-y-1.5">
                        <span class="text-xs font-medium text-muted-foreground"
                          >Full Name</span
                        >
                        <Input
                          bind:value={user.fullName}
                          class="h-9 bg-background/50"
                        />
                      </div>
                      <div class="space-y-1.5">
                        <span class="text-xs font-medium text-muted-foreground"
                          >Username</span
                        >
                        <Input
                          bind:value={user.userName}
                          readonly={!isNewUser}
                          placeholder={isNewUser ? "e.g., JDoe" : ""}
                          class="h-9 bg-background/50"
                        />
                      </div>
                      <div class="space-y-1.5">
                        <span class="text-xs font-medium text-muted-foreground"
                          >Email</span
                        >
                        <Input
                          bind:value={user.email}
                          class="h-9 bg-background/50"
                        />
                      </div>
                      <div class="space-y-1.5">
                        <span class="text-xs font-medium text-muted-foreground"
                          >Phone</span
                        >
                        <Input
                          bind:value={user.mobile}
                          class="h-9 bg-background/50"
                        />
                      </div>
                      <!-- Bio removed -->
                    </div>

                    <Separator class="my-4 opacity-50" />

                    <Separator class="my-4 opacity-50" />

                    <!-- Permissions / Settings Toggles -->
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      <div class="space-y-3">
                        <div class="flex items-center gap-2">
                          <Checkbox
                            id="chk-erp"
                            checked={user.canRunErp === 1}
                            onCheckedChange={(v) =>
                              (user.canRunErp = v ? 1 : 0)}
                          />
                          <label
                            for="chk-erp"
                            class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                          >
                            Can Run ERP
                          </label>
                        </div>
                        <div class="flex items-center gap-2">
                          <Checkbox
                            id="chk-old-erp"
                            checked={user.canRunOldErp === 1}
                            onCheckedChange={(v) =>
                              (user.canRunOldErp = v ? 1 : 0)}
                          />
                          <label
                            for="chk-old-erp"
                            class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                          >
                            Can Run Old ERP
                          </label>
                        </div>
                        <div class="flex items-center gap-2">
                          <Checkbox
                            id="chk-masters"
                            checked={user.allowAllMasters === 1}
                            onCheckedChange={(v) =>
                              (user.allowAllMasters = v ? 1 : 0)}
                          />
                          <label
                            for="chk-masters"
                            class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                          >
                            Allow All Masters
                          </label>
                        </div>
                      </div>

                      <div class="space-y-1.5">
                        <span class="text-xs font-medium text-muted-foreground"
                          >User Type</span
                        >
                        <Select.Root type="single" bind:value={user.userType}>
                          <Select.Trigger class="h-9 bg-background/50">
                            {user.userType || "Select"}
                          </Select.Trigger>
                          <Select.Content>
                            <Select.Item value="Admin">Admin</Select.Item>
                            <Select.Item value="Manager">Manager</Select.Item>
                            <Select.Item value="User">User</Select.Item>
                          </Select.Content>
                        </Select.Root>
                      </div>
                    </div>

                    <Separator class="my-4 opacity-50" />

                    <!-- Google Drive Settings (Nav Live User: Backup Storage Quota, Allowed File Types, G Drive Folder ID) -->
                    <div class="space-y-4">
                      <div>
                        <h3 class="text-sm font-semibold text-foreground">
                          Google Drive backup / sync
                        </h3>
                        <p class="text-xs text-muted-foreground mt-1">
                          Stored on the Nav <code class="text-[10px]">User</code> record. Used by the desktop sync client and <code class="text-[10px]">getDriveSyncConfig</code>.
                        </p>
                      </div>
                      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <div class="space-y-1.5">
                          <span
                            class="text-xs font-medium text-muted-foreground"
                            >Backup Storage Quota (GB)</span
                          >
                          <Input
                            type="number"
                            step="0.1"
                            bind:value={user.backupStorageQuotaGb}
                            class="h-9 bg-background/50"
                          />
                        </div>
                        <div class="space-y-1.5">
                          <span
                            class="text-xs font-medium text-muted-foreground"
                            >Allowed File Types</span
                          >
                          <Input
                            bind:value={user.backupAllowedFileTypes}
                            placeholder=".pdf,.docx,..."
                            class="h-9 bg-background/50"
                          />
                        </div>
                        <div class="space-y-1.5 sm:col-span-2">
                          <span
                            class="text-xs font-medium text-muted-foreground"
                            >Backup Folder ID</span
                          >
                          <Input
                            bind:value={user.backupGDriveFolderId}
                            class="h-9 bg-background/50"
                          />
                        </div>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </div>
            </Tabs.Content>

            <Tabs.Content value="resp-setup">
              <div in:slide={{ axis: "y", duration: 300 }}>
                <Card class="border-border/40 shadow-none bg-card/40">
                  <CardHeader class="pb-4">
                    <CardTitle class="text-base"
                      >Responsibility Center Setup</CardTitle
                    >
                    <CardDescription>
                      Assign responsibility centers to this user. Set resp
                      center, default, type, and code per entry.
                    </CardDescription>
                  </CardHeader>
                  <CardContent class="space-y-4">
                    {#if user.respCenterSetup.length === 0}
                      <div
                        class="flex flex-col items-center justify-center py-12 text-center text-muted-foreground border-2 border-dashed border-border/50 rounded-xl bg-muted/5 transition-colors hover:bg-muted/10 hover:border-primary/20 cursor-default"
                        in:fade
                      >
                        <div
                          class="p-4 bg-background rounded-full shadow-sm mb-3 ring-1 ring-border/50"
                        >
                          <Icon
                            name="layout-grid"
                            class="size-6 text-primary/60"
                          />
                        </div>
                        <h3 class="text-sm font-medium text-foreground">
                          No Responsibility Centers
                        </h3>
                        <p
                          class="text-xs text-muted-foreground mt-1 mb-4 max-w-xs mx-auto"
                        >
                          Assign responsibility centers to define the
                          operational scope regarding data visibility and
                          access.
                        </p>
                        <Button
                          variant="outline"
                          size="sm"
                          onclick={addRespCenter}
                          class="group shadow-sm"
                        >
                          <Icon
                            name="plus"
                            class="size-4 mr-2 group-hover:scale-110 transition-transform text-primary"
                          />
                          Add Assignment
                        </Button>
                      </div>
                    {:else}
                      <div class="space-y-3">
                        <!-- Desktop Header -->
                        <div
                          class="hidden md:grid grid-cols-12 gap-4 px-4 py-2 text-[10px] uppercase tracking-wider font-semibold text-muted-foreground/70 select-none"
                        >
                          <div class="col-span-4 pl-1">
                            Responsibility Center
                          </div>
                          <div class="col-span-4">Employee Code</div>
                          <div class="col-span-2">Type</div>
                          <div class="col-span-2 text-right pr-1">Actions</div>
                        </div>

                        {#each user.respCenterSetup as rc, i (rc)}
                          <div
                            animate:flip={{ duration: 300 }}
                            in:slide={{ axis: "y", duration: 300 }}
                            out:slide={{ axis: "x", duration: 200 }}
                            class="group relative grid grid-cols-1 md:grid-cols-12 gap-4 p-4 rounded-xl border transition-all duration-300 items-start md:items-center
                            {rc.default
                              ? 'bg-primary/5 border-primary/40 shadow-[0_0_15px_rgba(0,0,0,0.03)]'
                              : 'bg-card/40 border-border/50 hover:bg-card hover:border-border/80 hover:shadow-sm'}"
                          >
                            <!-- Selection Marker for Default -->
                            {#if rc.default}
                              <div
                                class="absolute inset-y-0 left-0 w-1 bg-primary rounded-l-xl"
                                in:fade
                              ></div>
                            {/if}

                            <!-- Resp Center -->
                            <div
                              class="col-span-1 md:col-span-4 space-y-1.5 md:space-y-0"
                            >
                              <label
                                class="block md:hidden text-xs font-medium text-muted-foreground"
                                >Responsibility Center</label
                              >
                              <VenSelect
                                options={respCenterSelectOptions}
                                bind:value={rc.respCenter}
                                valueKey="code"
                                labelKey="name"
                                placeholder="Select center..."
                                searchPlaceholder="Search..."
                                class="h-9 w-full bg-background/80 focus-within:bg-background transition-colors"
                              />
                            </div>

                            <!-- Employee / Code -->
                            <div
                              class="col-span-1 md:col-span-4 space-y-1.5 md:space-y-0"
                            >
                              <label
                                class="block md:hidden text-xs font-medium text-muted-foreground"
                                >Employee Code</label
                              >
                              <VenSelect
                                options={employeeSelectOptions}
                                bind:value={rc.code}
                                valueKey="no"
                                labelKey="label"
                                placeholder="Select employee..."
                                searchPlaceholder="Search employee..."
                                class="h-9 w-full bg-background/80 focus-within:bg-background font-mono text-sm transition-colors"
                              />
                            </div>

                            <!-- Type -->
                            <div
                              class="col-span-1 md:col-span-2 space-y-1.5 md:space-y-0"
                            >
                              <label
                                class="block md:hidden text-xs font-medium text-muted-foreground"
                                >Type</label
                              >
                              <VenSelect
                                options={[...TYPE_OPTIONS]}
                                bind:value={rc.type}
                                valueKey="value"
                                labelKey="label"
                                placeholder="Type"
                                searchPlaceholder="Type"
                                class="h-9 w-full bg-background/80 focus-within:bg-background font-mono transition-colors"
                              />
                            </div>

                            <!-- Actions -->
                            <div
                              class="col-span-1 md:col-span-2 flex flex-row items-center justify-between md:justify-end gap-2 pt-2 md:pt-0 border-t md:border-t-0 border-border/50 mt-2 md:mt-0"
                            >
                              <!-- Default Toggle (Star) -->
                              <div class="flex items-center">
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  class="h-8 gap-2 px-2 {rc.default
                                    ? 'text-amber-500 hover:text-amber-600 hover:bg-amber-500/10'
                                    : 'text-muted-foreground hover:text-amber-500'}"
                                  title={rc.default
                                    ? "Primary Responsibility Center"
                                    : "Set as Default"}
                                  onclick={() => {
                                    if (!rc.default) {
                                      // Unset others
                                      user.respCenterSetup.forEach(
                                        (r) => (r.default = 0),
                                      );
                                      rc.default = 1;
                                    } else {
                                      rc.default = 0;
                                    }
                                  }}
                                >
                                  <Icon
                                    name="star"
                                    class="size-4 {rc.default
                                      ? 'fill-current'
                                      : ''}"
                                  />
                                  <span class="md:hidden text-xs"
                                    >Set Default</span
                                  >
                                </Button>
                              </div>

                              <!-- Remove Button -->
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                class="h-8 w-8 text-muted-foreground hover:text-destructive hover:bg-destructive/10 transition-colors"
                                onclick={() => removeRespCenter(i)}
                                title="Remove assignment"
                              >
                                <Icon name="trash-2" class="size-4" />
                              </Button>
                            </div>
                          </div>
                        {/each}

                        <Button
                          variant="ghost"
                          class="w-full border border-dashed border-border/60 bg-muted/5 hover:bg-muted/30 text-muted-foreground hover:text-primary hover:border-primary/30 transition-all h-10 mt-2 gap-2"
                          onclick={addRespCenter}
                        >
                          <div
                            class="flex items-center justify-center size-5 rounded-full bg-primary/10 text-primary"
                          >
                            <Icon name="plus" class="size-3" />
                          </div>
                          Add Another Assignment
                        </Button>
                      </div>
                    {/if}
                  </CardContent>
                </Card>
              </div>
            </Tabs.Content>

            <Tabs.Content value="posting-setup">
              <div in:slide={{ axis: "y", duration: 300 }}>
                <Card class="border-border/40 shadow-none bg-card/40">
                  <CardHeader class="pb-4">
                    <CardTitle class="text-base">Posting Setup</CardTitle>
                    <CardDescription>
                      Configure posting date ranges per responsibility center (User Setup).
                    </CardDescription>
                  </CardHeader>
                  <CardContent class="space-y-4">
                    {#if user.postingSetup.length === 0}
                      <div
                        class="flex flex-col items-center justify-center py-12 text-center text-muted-foreground border-2 border-dashed border-border/50 rounded-xl bg-muted/5 transition-colors hover:bg-muted/10 hover:border-primary/20 cursor-default"
                        in:fade
                      >
                        <div class="p-4 bg-background rounded-full shadow-sm mb-3 ring-1 ring-border/50">
                          <Icon name="calendar" class="size-6 text-primary/60" />
                        </div>
                        <h3 class="text-sm font-medium text-foreground">No Posting Setup</h3>
                        <p class="text-xs text-muted-foreground mt-1 mb-4 max-w-xs mx-auto">
                          Add posting date ranges per responsibility center to control when this user can post.
                        </p>
                        <Button variant="outline" size="sm" onclick={addPostingSetup} class="group shadow-sm">
                          <Icon name="plus" class="size-4 mr-2 group-hover:scale-110 transition-transform text-primary" />
                          Add Entry
                        </Button>
                      </div>
                    {:else}
                      <div class="space-y-3">
                        <div class="hidden md:grid grid-cols-12 gap-4 px-4 py-2 text-[10px] uppercase tracking-wider font-semibold text-muted-foreground/70 select-none">
                          <div class="col-span-4 pl-1">Responsibility Center</div>
                          <div class="col-span-6">Posting Date Range</div>
                          <div class="col-span-2 text-right pr-1">Actions</div>
                        </div>

                        {#each user.postingSetup as ps, i (ps)}
                          <div
                            animate:flip={{ duration: 300 }}
                            in:slide={{ axis: "y", duration: 300 }}
                            out:slide={{ axis: "x", duration: 200 }}
                            class="group relative grid grid-cols-1 md:grid-cols-12 gap-4 p-4 rounded-xl border bg-card/40 border-border/50 hover:bg-card hover:border-border/80 transition-all items-start md:items-center"
                          >
                            <div class="col-span-1 md:col-span-4 space-y-1.5 md:space-y-0">
                              <label class="block md:hidden text-xs font-medium text-muted-foreground">Responsibility Center</label>
                              <VenSelect
                                options={respCenterSelectOptions}
                                bind:value={ps.responsibilityCenter}
                                valueKey="code"
                                labelKey="name"
                                placeholder="Select center..."
                                searchPlaceholder="Search..."
                                class="h-9 w-full bg-background/80"
                              />
                            </div>
                            <div class="col-span-1 md:col-span-6 space-y-1.5 md:space-y-0">
                              <label class="block md:hidden text-xs font-medium text-muted-foreground">Posting Date Range</label>
                              <DatePicker
                                mode="range"
                                valueType="text"
                                valueFormat="yyyy-MM-dd"
                                placeholder="Select date range..."
                                value={{ start: ps.allowPostingFrom || '', end: ps.allowPostingTo || '' }}
                                onValueChange={(v) => {
                                  if (v?.start != null) ps.allowPostingFrom = v.start ?? '';
                                  if (v?.end != null) ps.allowPostingTo = v.end ?? '';
                                  user.postingSetup = [...user.postingSetup];
                                }}
                              />
                            </div>
                            <div class="col-span-1 md:col-span-2 flex justify-end pt-2 md:pt-0 border-t md:border-t-0 border-border/50 mt-2 md:mt-0">
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                class="h-8 w-8 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                                onclick={() => removePostingSetup(i)}
                                title="Remove"
                              >
                                <Icon name="trash-2" class="size-4" />
                              </Button>
                            </div>
                          </div>
                        {/each}

                        <Button
                          variant="ghost"
                          class="w-full border border-dashed border-border/60 bg-muted/5 hover:bg-muted/30 text-muted-foreground hover:text-primary hover:border-primary/30 transition-all h-10 mt-2 gap-2"
                          onclick={addPostingSetup}
                        >
                          <div class="flex items-center justify-center size-5 rounded-full bg-primary/10 text-primary">
                            <Icon name="plus" class="size-3" />
                          </div>
                          Add Another Entry
                        </Button>
                      </div>
                    {/if}
                  </CardContent>
                </Card>
              </div>
            </Tabs.Content>

            <Tabs.Content value="permissions">
              <div in:slide={{ axis: "y", duration: 300 }}>
                <Card
                  class="border-border/40 shadow-sm bg-card/60 backdrop-blur-[2px] overflow-hidden"
                >
                  <CardHeader class="pb-3 border-b border-border/40 bg-muted/5">
                    <div class="flex items-center justify-between">
                      <div class="space-y-1">
                        <CardTitle
                          class="text-base font-semibold flex items-center gap-2"
                        >
                          <div
                            class="p-1.5 rounded-md bg-primary/10 text-primary"
                          >
                            <Icon name="shield-check" class="size-4" />
                          </div>
                          Permissions & Access
                        </CardTitle>
                        <CardDescription class="text-xs text-muted-foreground">
                          Manage the security roles and data scopes assigned to
                          this user.
                        </CardDescription>
                      </div>
                      <Button
                        size="sm"
                        variant="default"
                        class="h-8 gap-2 shadow-sm"
                        onclick={addPermission}
                      >
                        <Icon name="plus" class="size-3.5" />
                        <span class="hidden sm:inline">Add Role</span>
                      </Button>
                    </div>
                  </CardHeader>

                  {#if user.permissions.length === 0}
                    <div class="p-8 flex flex-col items-center text-center">
                      <div
                        class="size-16 rounded-full bg-muted/30 flex items-center justify-center mb-4 ring-1 ring-border/50"
                      >
                        <Icon
                          name="shield-alert"
                          class="size-8 text-muted-foreground/40"
                        />
                      </div>
                      <h3 class="text-sm font-medium text-foreground">
                        No Permissions Configured
                      </h3>
                      <p
                        class="text-xs text-muted-foreground max-w-[280px] mt-1.5 mb-6"
                      >
                        This user currently has no access rights. Assign a role
                        to verify their access level.
                      </p>
                      <Button variant="outline" onclick={addPermission}>
                        Assign First Permission
                      </Button>
                    </div>
                  {:else}
                    <div class="flex flex-col">
                      {#if permissionOptions.length === 0 && !loading}
                        <div
                          class="mx-4 mt-4 p-3 rounded-md bg-amber-500/10 border border-amber-500/20 flex items-start gap-3 text-amber-600 dark:text-amber-500 text-xs"
                        >
                          <Icon
                            name="triangle-triangle"
                            class="size-4 shrink-0 mt-0.5"
                          />
                          <div>
                            <p class="font-medium">
                              Permissions list unavailable
                            </p>
                            <p class="opacity-90">
                              We couldn't load the list of available roles.
                              Please check your connection/backend logs.
                            </p>
                          </div>
                        </div>
                      {/if}

                      <!-- Desktop Header -->
                      <div
                        class="hidden md:grid grid-cols-12 gap-4 px-6 py-3 border-b border-border/40 bg-muted/40 text-[10px] font-semibold text-muted-foreground uppercase tracking-wider"
                      >
                        <div class="col-span-5 pl-2">Role / Permission Set</div>
                        <div class="col-span-5">Scope Configuration</div>
                        <div class="col-span-2 text-right pr-2">Actions</div>
                      </div>

                      <div class="divide-y divide-border/40 bg-card/20">
                        {#each user.permissions as perm, i (perm)}
                          <div
                            animate:flip={{ duration: 300 }}
                            in:slide={{ axis: "y", duration: 200 }}
                            out:slide={{ axis: "x", duration: 200 }}
                            class="group relative grid grid-cols-1 md:grid-cols-12 gap-4 p-4 md:px-6 md:py-3.5 items-start md:items-center transition-all duration-200
                            {perm.homePath
                              ? 'bg-primary/5 border-primary/40 shadow-[0_0_15px_rgba(0,0,0,0.03)]'
                              : 'bg-card/40 border-border/50 hover:bg-card hover:border-border/80 hover:shadow-sm'}"
                          >
                            <!-- Role Selection -->
                            <div class="col-span-1 md:col-span-5 relative">
                              <!-- Selection Marker for Home/Default -->
                              {#if perm.homePath}
                                <div
                                  class="absolute inset-y-0 -left-6 md:-left-6 w-1 bg-primary rounded-l-xl"
                                  in:fade
                                ></div>
                              {/if}
                              <label
                                class="md:hidden text-xs font-medium text-muted-foreground block mb-2"
                                >Role</label
                              >
                              <div class="flex items-center gap-3">
                                <div
                                  class="hidden md:flex flex-col items-center gap-1"
                                >
                                  <div
                                    class="size-2 rounded-full {perm.roleId
                                      ? 'bg-primary shadow-[0_0_8px_rgba(var(--primary),0.5)]'
                                      : 'bg-muted-foreground/30'}"
                                  ></div>
                                  <div
                                    class="w-px h-8 bg-border/50 group-last:hidden"
                                  ></div>
                                </div>
                                <div class="w-full space-y-1">
                                  <VenSelect
                                    options={permissionSelectOptions}
                                    bind:value={perm.roleId}
                                    valueKey="roleId"
                                    labelKey="name"
                                    placeholder="Select a role..."
                                    searchPlaceholder="Search available roles..."
                                    class="w-full h-9 border-border/60 bg-background/50 focus:bg-background transition-all"
                                    onSelect={(val) => {
                                      const match = permissionOptions.find(
                                        (p: { roleId: string; name: string }) => p.roleId === val?.roleId,
                                      );
                                      perm.roleName = match ? match.name : "";

                                      // Check if we need to load reports for this new role
                                      if (
                                        val?.roleId &&
                                        REPORT_ROLES[val.roleId]
                                      ) {
                                        loadReportsIfNeeded(val.roleId);
                                        perm._values = []; // Reset values when changing role type
                                      }
                                    }}
                                  />
                                </div>
                              </div>
                            </div>

                            <!-- Scope / Values / Path -->
                            <div
                              class="col-span-1 md:col-span-5 flex flex-col md:flex-row gap-3"
                            >
                              <div class="w-full space-y-1.5 md:space-y-0">
                                <label
                                  class="md:hidden text-xs font-medium text-muted-foreground block"
                                  >Scope Values</label
                                >
                                <div class="relative group/input">
                                  {#if !!REPORT_ROLES[perm.roleId]}
                                    <VenSelect
                                      multiple
                                      options={reportsCache[
                                        REPORT_ROLES[perm.roleId]
                                      ] ?? []}
                                      bind:value={perm._values}
                                      valueKey="id"
                                      labelKey="name"
                                      placeholder="Select reports..."
                                      searchPlaceholder="Search reports..."
                                      class="h-min min-h-9 w-full rounded-lg bg-muted/40 border-transparent group-hover/input:border-border/60 focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-primary/10 transition-all text-xs"
                                      onSelect={() => {
                                        perm.values = perm._values
                                          .map(String)
                                          .join(",");
                                        user.permissions = [
                                          ...user.permissions,
                                        ];
                                      }}
                                    />
                                  {:else}
                                    <Input
                                      bind:value={perm.values}
                                      placeholder="Global scope"
                                      class="h-9 md:h-9 pl-8 text-xs font-mono bg-muted/40 border-transparent group-hover/input:border-border/60 focus:bg-background focus:border-primary/30 transition-all placeholder:text-muted-foreground/50"
                                    />
                                    <Icon
                                      name="database"
                                      class="absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground/40 pointer-events-none"
                                    />
                                  {/if}
                                </div>
                              </div>
                            </div>

                            <!-- Actions -->
                            <div
                              class="col-span-1 md:col-span-2 flex justify-between md:justify-end items-center gap-2"
                            >
                              <Button
                                variant={perm.homePath ? "default" : "ghost"}
                                size="icon"
                                class="size-8 rounded-full transition-all duration-300 {perm.homePath
                                  ? 'shadow-sm ring-2 ring-primary/20 scale-100'
                                  : 'text-muted-foreground hover:text-primary hover:bg-primary/10'}"
                                title={perm.homePath
                                  ? "Start View (Active)"
                                  : "Set as Start View"}
                                onclick={() => {
                                  if (!perm.homePath) {
                                    // Unset others if we are setting this one
                                    user.permissions.forEach(
                                      (p) => (p.homePath = 0),
                                    );
                                    perm.homePath = 1;
                                  } else {
                                    perm.homePath = 0;
                                  }
                                }}
                              >
                                <Icon
                                  name="home"
                                  class="size-4 transition-transform duration-300 {perm.homePath
                                    ? 'scale-110'
                                    : ''}"
                                />
                              </Button>

                              <div
                                class="h-4 w-px bg-border/40 mx-1 hidden md:block"
                              ></div>

                              <Button
                                variant="ghost"
                                size="icon"
                                class="size-8 text-muted-foreground/60 hover:text-destructive hover:bg-destructive/10 transition-colors"
                                onclick={() => removePermission(i)}
                                title="Remove Role"
                              >
                                <Icon name="trash-2" class="size-4" />
                              </Button>
                            </div>
                          </div>
                        {/each}
                      </div>

                      <div class="p-2 border-t border-border/40 bg-muted/20">
                        <Button
                          variant="ghost"
                          class="w-full text-xs text-muted-foreground hover:text-primary h-9 gap-2 border border-dashed border-border/60 hover:border-primary/40 hover:bg-background/80 transition-all"
                          onclick={addPermission}
                        >
                          <Icon name="plus" class="size-3.5" />
                          Add Another Role
                        </Button>
                      </div>
                    </div>
                  {/if}
                </Card>
              </div>
            </Tabs.Content>
          </Tabs.Root>
        </div>
      </div>
    {/if}
  </main>
</div>
