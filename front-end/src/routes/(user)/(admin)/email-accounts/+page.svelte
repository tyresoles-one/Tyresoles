<script lang="ts">
  import { onMount } from "svelte";
  import { fade, slide } from "svelte/transition";
  import { getGraphQLClient } from "$lib/services/graphql/client";
  import { toast } from "$lib/components/venUI/toast";
  import { Icon } from "$lib/components/venUI/icon";
  import { Card, CardContent } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Badge } from "$lib/components/ui/badge";
  import {
    GET_EMAIL_ACCOUNTS,
    GET_EMAIL_ACCOUNT_DETAILS,
    CREATE_EMAIL_ACCOUNT,
    UPDATE_EMAIL_ACCOUNT,
    DELETE_EMAIL_ACCOUNT,
    CHANGE_EMAIL_ACCOUNT_PASSWORD,
    UPDATE_EMAIL_ACCOUNT_STATUS,
    ADD_GLOBAL_ADDRESS_CONTACT,
  } from "./queries";

  interface EmailContact {
    nickname?: string;
    email?: string;
    firstName?: string;
    lastName?: string;
    department?: string;
    designation?: string;
    code?: string;
    mobile?: string;
    status?: string;
  }

  interface DetailedContact {
    email?: string;
    fname?: string;
    sname?: string;
    nickname?: string;
    code?: string;
    day?: string;
    month?: string;
    year?: string;
    branch?: string;
    mobile?: string;
    city?: string;
    altemail?: string;
    status?: string;
    designation?: string;
    department?: string;
    role?: string;
    org_name?: string;
    url?: string;
    note?: string;
    timezone?: string;
    address?: string;
    state?: string;
    zip?: string;
    country_code?: string;
    ph_work?: string;
    ph_home?: string;
    fax?: string;
  }

  let activeTab = $state<"accounts" | "global">("accounts");
  let loading = $state(false);
  let search = $state("");
  let statusFilter = $state<"ALL" | "ACTIVE" | "INACTIVE">("ALL");

  let accounts = $state<EmailContact[]>([]);
  let selectedContactDetails = $state<DetailedContact | null>(null);

  // Dialog States
  let showCreateModal = $state(false);
  let showEditModal = $state(false);
  let showPasswordModal = $state(false);
  let showDetailsModal = $state(false);
  let showDeleteModal = $state(false);
  let showAddGlobalModal = $state(false);

  let targetContact = $state<EmailContact | null>(null);

  // Forms State
  let createForm = $state({
    userId: "",
    password: "",
    firstName: "",
    lastName: "",
    nickname: "",
    employeeCode: "",
    mobile: "",
    userSpaceMb: 1024,
    pwdChangeAtFirstLogin: "N",
    branch: "",
    city: "",
    altEmail: "",
    designation: "",
    department: "",
    address: "",
    state: "",
    zip: "",
  });

  let editForm = $state({
    userId: "",
    firstName: "",
    lastName: "",
    nickname: "",
    employeeCode: "",
    mobile: "",
    branch: "",
    city: "",
    altEmail: "",
    designation: "",
    department: "",
    address: "",
    state: "",
    zip: "",
  });

  let passwordForm = $state({
    userId: "",
    newPassword: "",
  });

  let globalContactForm = $state({
    email: "",
    firstName: "",
    lastName: "",
    nickname: "",
    mobile: "",
    designation: "",
    department: "",
    orgName: "",
    city: "",
    state: "",
  });

  let passwordVisible = $state(false);

  // Load Data
  async function loadData() {
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(GET_EMAIL_ACCOUNTS);
      if (res?.emailAccounts?.success) {
        accounts = res.emailAccounts.contacts || [];
      } else {
        toast.error(res?.emailAccounts?.error || "Failed to fetch email accounts");
      }
    } catch (err: any) {
      toast.error(err.message || "Error loading email accounts");
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    loadData();
  });

  // Filtered List
  const filteredAccounts = $derived.by(() => {
    return accounts.filter((item) => {
      const q = search.toLowerCase().trim();
      const matchesSearch =
        !q ||
        (item.email || "").toLowerCase().includes(q) ||
        (item.firstName || "").toLowerCase().includes(q) ||
        (item.lastName || "").toLowerCase().includes(q) ||
        (item.code || "").toLowerCase().includes(q) ||
        (item.department || "").toLowerCase().includes(q);

      const matchesStatus =
        statusFilter === "ALL" ||
        (statusFilter === "ACTIVE" && (item.status === "A" || !item.status)) ||
        (statusFilter === "INACTIVE" && item.status && item.status !== "A");

      return matchesSearch && matchesStatus;
    });
  });

  // Counters
  const totalCount = $derived(accounts.length);
  const activeCount = $derived(
    accounts.filter((a) => a.status === "A" || !a.status).length
  );
  const inactiveCount = $derived(
    accounts.filter((a) => a.status && a.status !== "A").length
  );

  // Helper Functions
  function getInitials(fn?: string, ln?: string) {
    const f = fn ? fn[0] : "";
    const l = ln ? ln[0] : "";
    return (f + l).toUpperCase() || "E";
  }

  function generatePassword() {
    const chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
    let pwd = "";
    for (let i = 0; i < 12; i++) {
      pwd += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return pwd;
  }

  // Action Handlers
  async function fetchDetails(contact: EmailContact) {
    targetContact = contact;
    showDetailsModal = true;
    selectedContactDetails = null;
    try {
      const client = await getGraphQLClient();
      const userId = contact.email ? contact.email.split("@")[0] : undefined;
      const res: any = await client.request(GET_EMAIL_ACCOUNT_DETAILS, {
        userId,
        employeeCode: contact.code,
      });
      if (res?.emailAccountDetails) {
        selectedContactDetails = res.emailAccountDetails;
      }
    } catch (err: any) {
      toast.error("Failed to load detailed profile: " + err.message);
    }
  }

  function openEditModal(contact: EmailContact) {
    targetContact = contact;
    const userId = contact.email ? contact.email.split("@")[0] : "";
    editForm = {
      userId,
      firstName: contact.firstName || "",
      lastName: contact.lastName || "",
      nickname: contact.nickname || "",
      employeeCode: contact.code || "",
      mobile: contact.mobile || "",
      branch: "",
      city: "",
      altEmail: "",
      designation: contact.designation || "",
      department: contact.department || "",
      address: "",
      state: "",
      zip: "",
    };
    showEditModal = true;
  }

  function openPasswordModal(contact: EmailContact) {
    targetContact = contact;
    const userId = contact.email ? contact.email.split("@")[0] : "";
    passwordForm = {
      userId,
      newPassword: generatePassword(),
    };
    showPasswordModal = true;
  }

  async function handleCreateAccount() {
    if (!createForm.userId || !createForm.password || !createForm.firstName || !createForm.lastName) {
      toast.error("Please fill in all mandatory fields (User ID, Password, Name)");
      return;
    }
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(CREATE_EMAIL_ACCOUNT, {
        input: {
          userId: createForm.userId.trim(),
          password: createForm.password,
          firstName: createForm.firstName.trim(),
          lastName: createForm.lastName.trim(),
          nickname: createForm.nickname || `${createForm.firstName} ${createForm.lastName}`,
          employeeCode: createForm.employeeCode.trim(),
          mobile: createForm.mobile.trim(),
          userSpaceMb: Number(createForm.userSpaceMb) || 1024,
          pwdChangeAtFirstLogin: createForm.pwdChangeAtFirstLogin,
          branch: createForm.branch,
          city: createForm.city,
          altEmail: createForm.altEmail,
          designation: createForm.designation,
          department: createForm.department,
          address: createForm.address,
          state: createForm.state,
          zip: createForm.zip,
        },
      });

      if (res?.createEmailAccount?.success) {
        toast.success("Email account created successfully!");
        showCreateModal = false;
        loadData();
      } else {
        toast.error(res?.createEmailAccount?.error || res?.createEmailAccount?.message || "Creation failed");
      }
    } catch (err: any) {
      toast.error(err.message || "Failed to create account");
    } finally {
      loading = false;
    }
  }

  async function handleUpdateAccount() {
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(UPDATE_EMAIL_ACCOUNT, {
        input: {
          userId: editForm.userId,
          firstName: editForm.firstName,
          lastName: editForm.lastName,
          nickname: editForm.nickname,
          employeeCode: editForm.employeeCode,
          mobile: editForm.mobile,
          branch: editForm.branch,
          city: editForm.city,
          altEmail: editForm.altEmail,
          designation: editForm.designation,
          department: editForm.department,
          address: editForm.address,
          state: editForm.state,
          zip: editForm.zip,
        },
      });

      if (res?.updateEmailAccount?.success) {
        toast.success("Email account updated successfully!");
        showEditModal = false;
        loadData();
      } else {
        toast.error(res?.updateEmailAccount?.error || "Update failed");
      }
    } catch (err: any) {
      toast.error(err.message || "Failed to update account");
    } finally {
      loading = false;
    }
  }

  async function handleChangePassword() {
    if (!passwordForm.newPassword) {
      toast.error("Password cannot be empty");
      return;
    }
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(CHANGE_EMAIL_ACCOUNT_PASSWORD, {
        input: {
          userId: passwordForm.userId,
          newPassword: passwordForm.newPassword,
        },
      });

      if (res?.changeEmailAccountPassword?.success) {
        toast.success("Password changed successfully!");
        showPasswordModal = false;
      } else {
        toast.error(res?.changeEmailAccountPassword?.error || "Password change failed");
      }
    } catch (err: any) {
      toast.error(err.message || "Failed to change password");
    } finally {
      loading = false;
    }
  }

  async function handleToggleStatus(contact: EmailContact, newStatus: "Active" | "Deactive") {
    if (!contact.email) return;
    const userId = contact.email.split("@")[0];
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(UPDATE_EMAIL_ACCOUNT_STATUS, {
        input: {
          userIds: [userId],
          employeeCodes: contact.code ? [contact.code] : [],
          status: newStatus,
        },
      });

      if (res?.updateEmailAccountStatus?.success) {
        toast.success(`Account status changed to ${newStatus}`);
        loadData();
      } else {
        toast.error(res?.updateEmailAccountStatus?.error || "Status update failed");
      }
    } catch (err: any) {
      toast.error(err.message || "Failed to update status");
    } finally {
      loading = false;
    }
  }

  async function handleDeleteAccount() {
    if (!targetContact?.email) return;
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(DELETE_EMAIL_ACCOUNT, {
        userEmail: targetContact.email,
      });

      if (res?.deleteEmailAccount?.success) {
        toast.success("Account deleted successfully!");
        showDeleteModal = false;
        loadData();
      } else {
        toast.error(res?.deleteEmailAccount?.error || "Deletion failed");
      }
    } catch (err: any) {
      toast.error(err.message || "Failed to delete account");
    } finally {
      loading = false;
    }
  }

  async function handleAddGlobalContact() {
    if (!globalContactForm.email || !globalContactForm.firstName || !globalContactForm.lastName) {
      toast.error("Please fill in required fields (Email, First Name, Last Name)");
      return;
    }
    loading = true;
    try {
      const client = await getGraphQLClient();
      const res: any = await client.request(ADD_GLOBAL_ADDRESS_CONTACT, {
        input: {
          email: globalContactForm.email,
          firstName: globalContactForm.firstName,
          lastName: globalContactForm.lastName,
          nickname: globalContactForm.nickname || `${globalContactForm.firstName} ${globalContactForm.lastName}`,
          mobile: globalContactForm.mobile,
          designation: globalContactForm.designation,
          department: globalContactForm.department,
          orgName: globalContactForm.orgName || "External Contact",
          city: globalContactForm.city,
          state: globalContactForm.state,
        },
      });

      if (res?.addGlobalAddressContact?.success) {
        toast.success("Global contact added successfully!");
        showAddGlobalModal = false;
        loadData();
      } else {
        toast.error(res?.addGlobalAddressContact?.error || "Failed to add contact");
      }
    } catch (err: any) {
      toast.error(err.message || "Error adding global contact");
    } finally {
      loading = false;
    }
  }
</script>

<div class="space-y-6 max-w-[1600px] mx-auto p-4 md:p-6" in:fade={{ duration: 300 }}>
  <!-- Header Title & Action Buttons -->
  <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-border/60 pb-5">
    <div>
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-primary/10 text-primary">
          <Icon name="mail" class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-extrabold tracking-tight">Email Account Management</h1>
          <p class="text-sm text-muted-foreground mt-0.5">
            Rediffmail Pro Domain Administration & Address Book Management
          </p>
        </div>
      </div>
    </div>

    <div class="flex items-center gap-3 flex-wrap">
      <Button variant="outline" onclick={loadData} disabled={loading} class="gap-2">
        <Icon name="rotate-cw" class={`size-4 ${loading ? 'animate-spin' : ''}`} />
        Refresh
      </Button>
      <Button
        variant="outline"
        onclick={() => (showAddGlobalModal = true)}
        class="gap-2 border-dashed border-primary/40 hover:bg-primary/5"
      >
        <Icon name="user-plus" class="size-4 text-primary" />
        Add Global Contact
      </Button>
      <Button onclick={() => (showCreateModal = true)} class="gap-2 shadow-lg shadow-primary/20">
        <Icon name="plus" class="size-4" />
        Create Email Account
      </Button>
    </div>
  </div>

  <!-- Summary Metric Cards -->
  <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
    <Card class="bg-gradient-to-br from-card to-muted/20 border-border/60 shadow-sm">
      <CardContent class="p-5 flex items-center justify-between">
        <div>
          <p class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Total Accounts</p>
          <p class="text-3xl font-black mt-1">{totalCount}</p>
        </div>
        <div class="p-3 rounded-xl bg-blue-500/10 text-blue-500">
          <Icon name="users" class="size-6" />
        </div>
      </CardContent>
    </Card>

    <Card class="bg-gradient-to-br from-card to-emerald-500/5 border-emerald-500/20 shadow-sm">
      <CardContent class="p-5 flex items-center justify-between">
        <div>
          <p class="text-xs font-semibold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">Active Mailboxes</p>
          <p class="text-3xl font-black text-emerald-600 dark:text-emerald-400 mt-1">{activeCount}</p>
        </div>
        <div class="p-3 rounded-xl bg-emerald-500/10 text-emerald-500">
          <Icon name="check-circle" class="size-6" />
        </div>
      </CardContent>
    </Card>

    <Card class="bg-gradient-to-br from-card to-amber-500/5 border-amber-500/20 shadow-sm">
      <CardContent class="p-5 flex items-center justify-between">
        <div>
          <p class="text-xs font-semibold uppercase tracking-wider text-amber-600 dark:text-amber-400">Inactive / Suspended</p>
          <p class="text-3xl font-black text-amber-600 dark:text-amber-400 mt-1">{inactiveCount}</p>
        </div>
        <div class="p-3 rounded-xl bg-amber-500/10 text-amber-500">
          <Icon name="user-x" class="size-6" />
        </div>
      </CardContent>
    </Card>

    <Card class="bg-gradient-to-br from-card to-purple-500/5 border-purple-500/20 shadow-sm">
      <CardContent class="p-5 flex items-center justify-between">
        <div>
          <p class="text-xs font-semibold uppercase tracking-wider text-purple-600 dark:text-purple-400">Address Book</p>
          <p class="text-3xl font-black text-purple-600 dark:text-purple-400 mt-1">{totalCount}</p>
        </div>
        <div class="p-3 rounded-xl bg-purple-500/10 text-purple-500">
          <Icon name="book-user" class="size-6" />
        </div>
      </CardContent>
    </Card>
  </div>

  <!-- Main Content Area: Tabs, Filter & Table -->
  <Card class="border-border/60 shadow-xl shadow-black/5 overflow-hidden">
    <div class="p-4 md:p-6 border-b border-border/40 bg-muted/10 space-y-4">
      <div class="flex flex-col sm:flex-row items-center justify-between gap-4">
        <!-- Tabs -->
        <div class="flex items-center gap-1 p-1 bg-muted/60 rounded-xl border border-border/40 w-full sm:w-auto">
          <button
            onclick={() => (activeTab = "accounts")}
            class={`px-4 py-2 rounded-lg text-xs font-bold transition-all flex-1 sm:flex-initial flex items-center justify-center gap-2 ${
              activeTab === "accounts"
                ? "bg-card text-primary shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            <Icon name="mail" class="size-4" />
            Domain Accounts ({totalCount})
          </button>
          <button
            onclick={() => (activeTab = "global")}
            class={`px-4 py-2 rounded-lg text-xs font-bold transition-all flex-1 sm:flex-initial flex items-center justify-center gap-2 ${
              activeTab === "global"
                ? "bg-card text-primary shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            <Icon name="book-user" class="size-4" />
            Global Address Book
          </button>
        </div>

        <!-- Filter controls -->
        <div class="flex items-center gap-3 w-full sm:w-auto">
          <div class="relative flex-1 sm:w-72">
            <Icon name="search" class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
            <Input
              type="text"
              placeholder="Search by name, email, code..."
              bind:value={search}
              class="pl-9 text-xs"
            />
          </div>

          <select
            bind:value={statusFilter}
            class="px-3 py-2 text-xs font-medium rounded-md border border-input bg-background hover:bg-accent focus:outline-none"
          >
            <option value="ALL">All Statuses</option>
            <option value="ACTIVE">Active Only</option>
            <option value="INACTIVE">Inactive Only</option>
          </select>
        </div>
      </div>
    </div>

    <!-- Table -->
    <div class="overflow-x-auto">
      {#if loading && accounts.length === 0}
        <div class="p-12 text-center text-muted-foreground">
          <Icon name="rotate-cw" class="size-8 animate-spin mx-auto mb-3 text-primary" />
          <p class="text-sm font-medium">Fetching Rediffmail accounts...</p>
        </div>
      {:else if filteredAccounts.length === 0}
        <div class="p-12 text-center text-muted-foreground">
          <Icon name="mail" class="size-10 mx-auto mb-3 opacity-40" />
          <p class="text-sm font-bold text-foreground">No accounts found</p>
          <p class="text-xs mt-1">Try adjusting your search filter or create a new account.</p>
        </div>
      {:else}
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="border-b border-border/40 bg-muted/20 text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
              <th class="py-3 px-4">User / Email</th>
              <th class="py-3 px-4">Employee Code</th>
              <th class="py-3 px-4">Department / Role</th>
              <th class="py-3 px-4">Status</th>
              <th class="py-3 px-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border/30 text-xs font-medium">
            {#each filteredAccounts as item}
              <tr class="hover:bg-muted/30 transition-colors group">
                <td class="py-3.5 px-4">
                  <div class="flex items-center gap-3">
                    <div class="size-9 rounded-full bg-primary/10 text-primary font-bold flex items-center justify-center text-xs shrink-0">
                      {getInitials(item.firstName, item.lastName)}
                    </div>
                    <div>
                      <div class="font-bold text-foreground flex items-center gap-2">
                        {item.firstName || ''} {item.lastName || ''}
                        {#if item.nickname && item.nickname !== `${item.firstName} ${item.lastName}`}
                          <span class="text-[10px] text-muted-foreground font-normal">({item.nickname})</span>
                        {/if}
                      </div>
                      <div class="text-muted-foreground font-mono text-[11px] mt-0.5">{item.email}</div>
                    </div>
                  </div>
                </td>

                <td class="py-3.5 px-4 font-mono text-muted-foreground">
                  {item.code || '-'}
                </td>

                <td class="py-3.5 px-4">
                  <div class="font-semibold text-foreground">{item.department || item.designation || '-'}</div>
                  {#if item.designation && item.department}
                    <div class="text-[10px] text-muted-foreground">{item.designation}</div>
                  {/if}
                </td>

                <td class="py-3.5 px-4">
                  {#if item.status === "A" || !item.status}
                    <Badge variant="outline" class="bg-emerald-500/10 text-emerald-600 border-emerald-500/30 gap-1.5 py-0.5">
                      <span class="size-1.5 rounded-full bg-emerald-500"></span>
                      Active
                    </Badge>
                  {:else}
                    <Badge variant="outline" class="bg-amber-500/10 text-amber-600 border-amber-500/30 gap-1.5 py-0.5">
                      <span class="size-1.5 rounded-full bg-amber-500"></span>
                      Inactive
                    </Badge>
                  {/if}
                </td>

                <td class="py-3.5 px-4 text-right">
                  <div class="flex items-center justify-end gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => fetchDetails(item)}
                      title="View Full Profile"
                      class="size-8 hover:bg-primary/10 hover:text-primary"
                    >
                      <Icon name="eye" class="size-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => openEditModal(item)}
                      title="Edit Account"
                      class="size-8 hover:bg-blue-500/10 hover:text-blue-500"
                    >
                      <Icon name="pencil" class="size-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => openPasswordModal(item)}
                      title="Change Password"
                      class="size-8 hover:bg-purple-500/10 hover:text-purple-500"
                    >
                      <Icon name="key-round" class="size-4" />
                    </Button>
                    {#if item.status === "A" || !item.status}
                      <Button
                        variant="ghost"
                        size="icon"
                        onclick={() => handleToggleStatus(item, "Deactive")}
                        title="Deactivate Account"
                        class="size-8 hover:bg-amber-500/10 hover:text-amber-500"
                      >
                        <Icon name="pause-circle" class="size-4" />
                      </Button>
                    {:else}
                      <Button
                        variant="ghost"
                        size="icon"
                        onclick={() => handleToggleStatus(item, "Active")}
                        title="Activate Account"
                        class="size-8 hover:bg-emerald-500/10 hover:text-emerald-500"
                      >
                        <Icon name="play-circle" class="size-4" />
                      </Button>
                    {/if}
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => {
                        targetContact = item;
                        showDeleteModal = true;
                      }}
                      title="Delete Account"
                      class="size-8 hover:bg-red-500/10 hover:text-red-500"
                    >
                      <Icon name="trash-2" class="size-4" />
                    </Button>
                  </div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      {/if}
    </div>
  </Card>
</div>

<!-- CREATE EMAIL ACCOUNT MODAL -->
{#if showCreateModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 overflow-y-auto" in:fade={{ duration: 150 }}>
    <div class="bg-card border border-border/60 rounded-2xl max-w-2xl w-full shadow-2xl overflow-hidden my-8" in:slide={{ duration: 200 }}>
      <div class="p-5 border-b border-border/40 bg-muted/20 flex items-center justify-between">
        <h3 class="font-bold text-base flex items-center gap-2">
          <Icon name="user-plus" class="size-5 text-primary" />
          Create New Email Account
        </h3>
        <button onclick={() => (showCreateModal = false)} class="p-1 hover:bg-muted rounded-lg text-muted-foreground">
          <Icon name="x" class="size-5" />
        </button>
      </div>

      <div class="p-6 space-y-4 max-h-[75vh] overflow-y-auto text-xs">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Email ID (Prefix) *</label>
            <Input placeholder="e.g. john.doe" bind:value={createForm.userId} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Password *</label>
            <div class="relative">
              <Input
                type={passwordVisible ? "text" : "password"}
                placeholder="Initial password"
                bind:value={createForm.password}
                class="text-xs pr-10"
              />
              <button
                type="button"
                onclick={() => (passwordVisible = !passwordVisible)}
                class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              >
                <Icon name={passwordVisible ? "eye-off" : "eye"} class="size-4" />
              </button>
            </div>
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">First Name *</label>
            <Input placeholder="First Name" bind:value={createForm.firstName} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Last Name *</label>
            <Input placeholder="Last Name" bind:value={createForm.lastName} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Employee Code *</label>
            <Input placeholder="e.g. EMP1024" bind:value={createForm.employeeCode} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Mobile Number *</label>
            <Input placeholder="10-digit mobile" bind:value={createForm.mobile} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Mailbox Space (MB)</label>
            <select bind:value={createForm.userSpaceMb} class="w-full px-3 py-2 rounded-md border border-input bg-background text-xs">
              <option value={500}>500 MB</option>
              <option value={1024}>1 GB (1024 MB)</option>
              <option value={2048}>2 GB (2048 MB)</option>
              <option value={5120}>5 GB (5120 MB)</option>
            </select>
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Alternate Email</label>
            <Input placeholder="Personal or backup email" bind:value={createForm.altEmail} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Department</label>
            <Input placeholder="e.g. Sales / Accounts" bind:value={createForm.department} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Designation</label>
            <Input placeholder="e.g. Manager" bind:value={createForm.designation} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Branch</label>
            <Input placeholder="Branch Name" bind:value={createForm.branch} class="text-xs" />
          </div>

          <div>
            <label class="font-semibold text-muted-foreground block mb-1">City</label>
            <Input placeholder="City" bind:value={createForm.city} class="text-xs" />
          </div>
        </div>
      </div>

      <div class="p-4 border-t border-border/40 bg-muted/20 flex justify-end gap-3">
        <Button variant="outline" onclick={() => (showCreateModal = false)}>Cancel</Button>
        <Button onclick={handleCreateAccount} disabled={loading} class="gap-2">
          {#if loading}
            <Icon name="rotate-cw" class="size-4 animate-spin" />
          {/if}
          Create Account
        </Button>
      </div>
    </div>
  </div>
{/if}

<!-- EDIT EMAIL ACCOUNT MODAL -->
{#if showEditModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 overflow-y-auto" in:fade={{ duration: 150 }}>
    <div class="bg-card border border-border/60 rounded-2xl max-w-xl w-full shadow-2xl overflow-hidden my-8" in:slide={{ duration: 200 }}>
      <div class="p-5 border-b border-border/40 bg-muted/20 flex items-center justify-between">
        <h3 class="font-bold text-base flex items-center gap-2">
          <Icon name="pencil" class="size-5 text-blue-500" />
          Edit Account: {editForm.userId}
        </h3>
        <button onclick={() => (showEditModal = false)} class="p-1 hover:bg-muted rounded-lg text-muted-foreground">
          <Icon name="x" class="size-5" />
        </button>
      </div>

      <div class="p-6 space-y-4 text-xs">
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">First Name</label>
            <Input bind:value={editForm.firstName} class="text-xs" />
          </div>
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Last Name</label>
            <Input bind:value={editForm.lastName} class="text-xs" />
          </div>
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Employee Code</label>
            <Input bind:value={editForm.employeeCode} class="text-xs" />
          </div>
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Mobile</label>
            <Input bind:value={editForm.mobile} class="text-xs" />
          </div>
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Department</label>
            <Input bind:value={editForm.department} class="text-xs" />
          </div>
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Designation</label>
            <Input bind:value={editForm.designation} class="text-xs" />
          </div>
        </div>
      </div>

      <div class="p-4 border-t border-border/40 bg-muted/20 flex justify-end gap-3">
        <Button variant="outline" onclick={() => (showEditModal = false)}>Cancel</Button>
        <Button onclick={handleUpdateAccount} disabled={loading} class="gap-2">
          {#if loading}
            <Icon name="rotate-cw" class="size-4 animate-spin" />
          {/if}
          Save Changes
        </Button>
      </div>
    </div>
  </div>
{/if}

<!-- CHANGE PASSWORD MODAL -->
{#if showPasswordModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4" in:fade={{ duration: 150 }}>
    <div class="bg-card border border-border/60 rounded-2xl max-w-md w-full shadow-2xl overflow-hidden" in:slide={{ duration: 200 }}>
      <div class="p-5 border-b border-border/40 bg-muted/20 flex items-center justify-between">
        <h3 class="font-bold text-base flex items-center gap-2">
          <Icon name="key-round" class="size-5 text-purple-500" />
          Change Password
        </h3>
        <button onclick={() => (showPasswordModal = false)} class="p-1 hover:bg-muted rounded-lg text-muted-foreground">
          <Icon name="x" class="size-5" />
        </button>
      </div>

      <div class="p-6 space-y-4 text-xs">
        <p class="text-muted-foreground">Setting new password for <span class="font-bold text-foreground">{passwordForm.userId}</span></p>

        <div>
          <label class="font-semibold text-muted-foreground block mb-1">New Password</label>
          <div class="flex items-center gap-2">
            <Input type="text" bind:value={passwordForm.newPassword} class="text-xs font-mono" />
            <Button
              type="button"
              variant="outline"
              size="icon"
              onclick={() => (passwordForm.newPassword = generatePassword())}
              title="Generate Random Password"
            >
              <Icon name="sparkles" class="size-4 text-purple-500" />
            </Button>
          </div>
        </div>
      </div>

      <div class="p-4 border-t border-border/40 bg-muted/20 flex justify-end gap-3">
        <Button variant="outline" onclick={() => (showPasswordModal = false)}>Cancel</Button>
        <Button onclick={handleChangePassword} disabled={loading} class="gap-2 bg-purple-600 hover:bg-purple-700 text-white">
          {#if loading}
            <Icon name="rotate-cw" class="size-4 animate-spin" />
          {/if}
          Update Password
        </Button>
      </div>
    </div>
  </div>
{/if}

<!-- VIEW PROFILE DETAILS MODAL -->
{#if showDetailsModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4" in:fade={{ duration: 150 }}>
    <div class="bg-card border border-border/60 rounded-2xl max-w-lg w-full shadow-2xl overflow-hidden" in:slide={{ duration: 200 }}>
      <div class="p-5 border-b border-border/40 bg-muted/20 flex items-center justify-between">
        <h3 class="font-bold text-base flex items-center gap-2">
          <Icon name="user" class="size-5 text-primary" />
          User Details Profile
        </h3>
        <button onclick={() => (showDetailsModal = false)} class="p-1 hover:bg-muted rounded-lg text-muted-foreground">
          <Icon name="x" class="size-5" />
        </button>
      </div>

      <div class="p-6 space-y-4 text-xs max-h-[70vh] overflow-y-auto">
        {#if !selectedContactDetails}
          <div class="p-8 text-center text-muted-foreground">
            <Icon name="rotate-cw" class="size-6 animate-spin mx-auto mb-2 text-primary" />
            Loading account details from Rediffmail...
          </div>
        {:else}
          <div class="space-y-3 divide-y divide-border/30">
            <div class="pb-2">
              <span class="text-muted-foreground">Email Address</span>
              <p class="font-bold font-mono text-sm text-primary">{selectedContactDetails.email || targetContact?.email}</p>
            </div>

            <div class="pt-2 grid grid-cols-2 gap-4">
              <div>
                <span class="text-muted-foreground">First Name</span>
                <p class="font-semibold">{selectedContactDetails.fname || '-'}</p>
              </div>
              <div>
                <span class="text-muted-foreground">Last Name</span>
                <p class="font-semibold">{selectedContactDetails.sname || '-'}</p>
              </div>
            </div>

            <div class="pt-2 grid grid-cols-2 gap-4">
              <div>
                <span class="text-muted-foreground">Employee Code</span>
                <p class="font-semibold font-mono">{selectedContactDetails.code || '-'}</p>
              </div>
              <div>
                <span class="text-muted-foreground">Mobile</span>
                <p class="font-semibold">{selectedContactDetails.mobile || '-'}</p>
              </div>
            </div>

            <div class="pt-2 grid grid-cols-2 gap-4">
              <div>
                <span class="text-muted-foreground">Department</span>
                <p class="font-semibold">{selectedContactDetails.department || '-'}</p>
              </div>
              <div>
                <span class="text-muted-foreground">Designation</span>
                <p class="font-semibold">{selectedContactDetails.designation || '-'}</p>
              </div>
            </div>

            <div class="pt-2 grid grid-cols-2 gap-4">
              <div>
                <span class="text-muted-foreground">Branch / City</span>
                <p class="font-semibold">{selectedContactDetails.branch || selectedContactDetails.city || '-'}</p>
              </div>
              <div>
                <span class="text-muted-foreground">Alt Email</span>
                <p class="font-semibold">{selectedContactDetails.altemail || '-'}</p>
              </div>
            </div>
          </div>
        {/if}
      </div>

      <div class="p-4 border-t border-border/40 bg-muted/20 flex justify-end">
        <Button variant="outline" onclick={() => (showDetailsModal = false)}>Close</Button>
      </div>
    </div>
  </div>
{/if}

<!-- ADD GLOBAL CONTACT MODAL -->
{#if showAddGlobalModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4" in:fade={{ duration: 150 }}>
    <div class="bg-card border border-border/60 rounded-2xl max-w-md w-full shadow-2xl overflow-hidden" in:slide={{ duration: 200 }}>
      <div class="p-5 border-b border-border/40 bg-muted/20 flex items-center justify-between">
        <h3 class="font-bold text-base flex items-center gap-2">
          <Icon name="user-plus" class="size-5 text-purple-500" />
          Add Contact to Global Address Book
        </h3>
        <button onclick={() => (showAddGlobalModal = false)} class="p-1 hover:bg-muted rounded-lg text-muted-foreground">
          <Icon name="x" class="size-5" />
        </button>
      </div>

      <div class="p-6 space-y-3 text-xs">
        <div>
          <label class="font-semibold text-muted-foreground block mb-1">Email Address *</label>
          <Input placeholder="contact@external.com" bind:value={globalContactForm.email} class="text-xs" />
        </div>
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">First Name *</label>
            <Input placeholder="First Name" bind:value={globalContactForm.firstName} class="text-xs" />
          </div>
          <div>
            <label class="font-semibold text-muted-foreground block mb-1">Last Name *</label>
            <Input placeholder="Last Name" bind:value={globalContactForm.lastName} class="text-xs" />
          </div>
        </div>
        <div>
          <label class="font-semibold text-muted-foreground block mb-1">Mobile</label>
          <Input placeholder="Mobile number" bind:value={globalContactForm.mobile} class="text-xs" />
        </div>
        <div>
          <label class="font-semibold text-muted-foreground block mb-1">Company / Org Name</label>
          <Input placeholder="Organization" bind:value={globalContactForm.orgName} class="text-xs" />
        </div>
      </div>

      <div class="p-4 border-t border-border/40 bg-muted/20 flex justify-end gap-3">
        <Button variant="outline" onclick={() => (showAddGlobalModal = false)}>Cancel</Button>
        <Button onclick={handleAddGlobalContact} disabled={loading}>Add Contact</Button>
      </div>
    </div>
  </div>
{/if}

<!-- DELETE ACCOUNT CONFIRMATION MODAL -->
{#if showDeleteModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4" in:fade={{ duration: 150 }}>
    <div class="bg-card border border-red-500/30 rounded-2xl max-w-md w-full shadow-2xl overflow-hidden" in:slide={{ duration: 200 }}>
      <div class="p-5 border-b border-border/40 bg-red-500/10 flex items-center justify-between">
        <h3 class="font-bold text-base text-red-600 dark:text-red-400 flex items-center gap-2">
          <Icon name="alert-triangle" class="size-5" />
          Delete Email Account
        </h3>
        <button onclick={() => (showDeleteModal = false)} class="p-1 hover:bg-muted rounded-lg text-muted-foreground">
          <Icon name="x" class="size-5" />
        </button>
      </div>

      <div class="p-6 space-y-3 text-xs">
        <p>Are you sure you want to permanently delete the email account <span class="font-bold text-foreground">{targetContact?.email}</span>?</p>
        <div class="p-3 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-700 dark:text-amber-300">
          <strong>Warning:</strong> This operation will permanently erase the mailbox and all associated email data from Rediffmail servers.
        </div>
      </div>

      <div class="p-4 border-t border-border/40 bg-muted/20 flex justify-end gap-3">
        <Button variant="outline" onclick={() => (showDeleteModal = false)}>Cancel</Button>
        <Button variant="destructive" onclick={handleDeleteAccount} disabled={loading}>
          {#if loading}
            <Icon name="rotate-cw" class="size-4 animate-spin" />
          {/if}
          Delete Permanently
        </Button>
      </div>
    </div>
  </div>
{/if}
