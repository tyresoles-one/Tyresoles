<script lang="ts">
  import { onMount } from "svelte";
  import {
    appConfigStore,
    type AppConfig,
  } from "$lib/config/runtime";
  import { authStore } from "$lib/stores/auth";
  import { isTauri, getInvoke } from "$lib/tauri";
  import { Button } from "$lib/components/ui/button";
  import { toast } from "$lib/components/venUI/toast";
  import {
    useGraphQLQuery,
    buildQuery,
    graphqlQuery,
  } from "$lib/services/graphql";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import { Icon } from "$lib/components/venUI/icon";
  import * as Dialog from "$lib/components/ui/dialog";
  import { Checkbox } from "$lib/components/ui/checkbox";

  let config = $state<AppConfig | null>(null);
  let loadingCode = $state<string | null>(null);
  let fixingRdp = $state(false);

  // RDP History state
  let historyDialogOpen = $state(false);
  let rdpHistory = $state<Record<string, string>>({});
  let loadingHistory = $state(false);
  let deletingHistory = $state(false);
  let selectedHistoryKeys = $state<string[]>([]);

  const GetGroupDetailsQuery = buildQuery`
    query GetGroupDetails($category: String!, $codes: String) {
      groupDetails(category: $category, codes: $codes) {
        code
        category
        name
        value
        extraValue
      }
    }
  `;

  const GetUserQuery = buildQuery`
    query GetUser($username: String!) {
      user(username: $username) {
        userId
        fullName
        rdpPassword
        navConfigName
      }
    }
  `;

  // Fetch RUNERP group details on mount
  const runErpResult = useGraphQLQuery<any>(GetGroupDetailsQuery, {
    variables: { category: "RUNERP" },
    cacheKey: "group_details_runerp",
  });

  onMount(() => {
    const unsub = appConfigStore.subscribe((c) => {
      config = c;
    });
    return () => {
      unsub();
    };
  });

  async function handleAction(code: string) {
    if (loadingCode) return;

    const c = config;
    const auth = authStore.get();
    const username = auth.username || auth.user?.userId || "";

    if (!username) {
      toast.error("You must be logged in to perform this action.");
      return;
    }

    if (!c) {
      toast.error("Configuration not loaded.");
      return;
    }

    loadingCode = code;

    try {
      if (isTauri()) {
        const invoke = getInvoke();
        if (!invoke) throw new Error("Tauri internals not found.");

        const userRes = await graphqlQuery<any>(GetUserQuery, {
          variables: { username },
        });

        if (!userRes.success || !userRes.data?.user) {
          throw new Error("Failed to fetch user connection details.");
        }

        const userDetails = userRes.data.user;

        if (c.mode === "User") {
          if (!c.rdpUrl) throw new Error("RDP URL is not configured.");

          await invoke("launch_rdp", {
            rdpUrl: c.rdpUrl,
            username: username,
            password: userDetails.rdpPassword || c.rdpPassword || "",
          });
          toast.success("Opening ERP (RDP)...");
        } else if (c.mode === "Server") {
          if (!c.navExePath) throw new Error("NAV Exe Path is not configured.");
          
          const navConfig = code === "2" ? "Old.config" : userDetails.navConfigName;
          
          if (!navConfig)
            throw new Error("NAV Config Name not found for user.");

          await invoke("launch_nav", {
            navExePath: c.navExePath,
            navConfigName: navConfig,
          });
          toast.success("Launching NAV Client...");
        }
      } else {
        if (code === "3") {
          if (!c.webErpUrl) throw new Error("Web ERP URL is not configured.");
          window.open(c.webErpUrl, "_blank", "noopener,noreferrer");
        } else {
          toast.info("This application is only available in the desktop app.");
        }
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Action failed.");
    } finally {
      loadingCode = null;
    }
  }

  async function handleFixRdpWarnings() {
    if (fixingRdp) return;
    if (!isTauri()) {
      toast.error("This action is only available in the desktop app.");
      return;
    }

    fixingRdp = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");

      await invoke("suppress_rdp_warnings");
      toast.success("RDP Warning Dialogs suppressed successfully.");
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg);
    } finally {
      fixingRdp = false;
    }
  }

  async function fetchRdpHistory() {
    if (!isTauri()) return;
    loadingHistory = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");

      const resultStr = await invoke<string>("get_rdp_history");
      if (resultStr && resultStr.trim() !== "") {
        const parsed = JSON.parse(resultStr);
        const filtered: Record<string, string> = {};
        for (const key in parsed) {
          if (key.startsWith("MRU") && parsed[key]) {
            filtered[key] = parsed[key];
          }
        }
        rdpHistory = filtered;
      } else {
        rdpHistory = {};
      }
    } catch (e) {
      console.error("Failed to fetch RDP history", e);
      rdpHistory = {};
    } finally {
      loadingHistory = false;
    }
  }

  async function handleDeleteRdpHistory() {
    if (deletingHistory || selectedHistoryKeys.length === 0) return;
    deletingHistory = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");

      await invoke("delete_rdp_history", { keys: selectedHistoryKeys });
      toast.success(`Deleted ${selectedHistoryKeys.length} history entries.`);
      selectedHistoryKeys = [];
      await fetchRdpHistory();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Failed to delete history.");
    } finally {
      deletingHistory = false;
    }
  }

  function handleOpenHistoryDialog() {
    historyDialogOpen = true;
    selectedHistoryKeys = [];
    fetchRdpHistory();
  }

  function getButtons() {
    if (!runErpResult.data?.groupDetails) return [];
    const details = runErpResult.data.groupDetails;
    const isDesktop = isTauri();

    if (isDesktop && config) {
      if (config.mode === "User") {
        return [{ name: "Open ERP (RDP)", code: "RDP" }];
      } else if (config.mode === "Server") {
        return details.filter((d: any) => d.code === "1" || d.code === "2");
      }
    } else if (!isDesktop && config) {
      return details.filter((d: any) => d.code === "3");
    }
    return [];
  }
  
  let allSelected = $derived(
    Object.keys(rdpHistory).length > 0 && selectedHistoryKeys.length === Object.keys(rdpHistory).length
  );
  
  function toggleAllHistory() {
    if (allSelected) {
      selectedHistoryKeys = [];
    } else {
      selectedHistoryKeys = Object.keys(rdpHistory);
    }
  }
  
  function toggleHistoryItem(key: string) {
    if (selectedHistoryKeys.includes(key)) {
      selectedHistoryKeys = selectedHistoryKeys.filter(k => k !== key);
    } else {
      selectedHistoryKeys = [...selectedHistoryKeys, key];
    }
  }
</script>

<div class="min-h-screen bg-muted/30 pb-16">
  <PageHeading
    backHref="/"
    backLabel="Back to Dashboard"
    icon="layout-dashboard"
    class="border-b bg-background"
  >
    {#snippet title()}
      ERP Applications
    {/snippet}
    {#snippet description()}
      Connect to your Enterprise Resource Planning suite securely
    {/snippet}
    {#snippet actions()}
      {#if config?.mode === "User" && isTauri()}
        <Button 
          variant="outline" 
          size="sm" 
          onclick={handleOpenHistoryDialog} 
          title="Manage RDP Connection History"
        >
          <Icon name="history" class="size-4 mr-2" />
          RDP History
        </Button>
        <Button 
          variant="outline" 
          size="sm" 
          onclick={handleFixRdpWarnings} 
          loading={fixingRdp}
          title="Run as Administrator to suppress RDP warnings"
        >
          <Icon name="shield-alert" class="size-4 mr-2" />
          Fix RDP Warnings
        </Button>
      {/if}
    {/snippet}
  </PageHeading>

  <main class="container mx-auto px-4 py-8 md:px-6">
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-xl font-semibold tracking-tight">Available Apps</h2>
      {#if runErpResult.loading}
        <div
          class="size-5 border-2 border-primary border-t-transparent rounded-full animate-spin"
          title="Loading available apps..."
        ></div>
      {/if}
    </div>

    {#if config === null}
      <div
        class="flex flex-col items-center justify-center py-16 gap-4 text-muted-foreground bg-card rounded-xl border shadow-sm"
      >
        <div
          class="size-10 border-4 border-muted border-t-primary rounded-full animate-spin"
        ></div>
        <p class="font-medium text-lg">Initializing configuration...</p>
      </div>
    {:else}
      <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {#each getButtons() as btn}
          <div
            class="group relative flex flex-col justify-between overflow-hidden rounded-xl border bg-card p-6 transition-all duration-300 hover:-translate-y-1 hover:border-primary/50 hover:shadow-lg"
          >
            <div>
              <div
                class="mb-5 flex size-12 items-center justify-center rounded-lg bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground"
              >
                {#if btn.code === "RDP"}
                  <Icon name="monitor" class="size-6" />
                {:else if btn.code === "1" || btn.code === "2"}
                  <Icon name="folder-open" class="size-6" />
                {:else if btn.code === "3"}
                  <Icon name="globe" class="size-6" />
                {:else}
                  <Icon name="app-window" class="size-6" />
                {/if}
              </div>
              <h3 class="mb-2 block text-xl font-semibold tracking-tight">
                {btn.name}
              </h3>
              <p class="mb-6 text-sm text-muted-foreground">
                {#if btn.code === "RDP"}
                  Connect to ERP natively via Remote Desktop seamlessly.
                {:else if btn.code === "1" || btn.code === "2"}
                  Launch the local Dynamics NAV instance directly on your
                  machine.
                {:else if btn.code === "3"}
                  Open the ERP portal via your default web browser.
                {/if}
              </p>
            </div>

            <Button
              variant="default"
              class="w-full gap-2 transition-all group-hover:shadow-md"
              onclick={() => handleAction(btn.code)}
              loading={loadingCode === btn.code}
            >
              Launch App <Icon name="arrow-right" class="size-4" />
            </Button>
          </div>
        {:else}
          {#if !runErpResult.loading}
            <div
              class="col-span-full py-16 text-center border-2 border-dashed rounded-xl bg-muted/20"
            >
              <div
                class="mx-auto mb-4 flex size-12 items-center justify-center rounded-full bg-muted"
              >
                <Icon
                  name="alert-circle"
                  class="size-6 text-muted-foreground"
                />
              </div>
              <h3 class="mb-1 text-lg font-semibold">
                No Applications Available
              </h3>
              <p class="text-muted-foreground">
                There are no ERP applications configured for your current
                environment mode ({config.mode}).
              </p>
            </div>
          {/if}
        {/each}
      </div>
    {/if}
  </main>
</div>

<Dialog.Root bind:open={historyDialogOpen}>
  <Dialog.Content class="sm:max-w-[425px]">
    <Dialog.Header>
      <Dialog.Title>RDP Connection History</Dialog.Title>
      <Dialog.Description>
        Manage the saved connection history in your Windows RDP Client.
      </Dialog.Description>
    </Dialog.Header>
    <div class="py-4">
      {#if loadingHistory}
        <div class="flex justify-center p-4">
          <div class="size-6 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
        </div>
      {:else if Object.keys(rdpHistory).length === 0}
        <div class="text-center text-sm text-muted-foreground py-8 border rounded-lg border-dashed">
          No history entries found.
        </div>
      {:else}
        <div class="space-y-4">
          <div class="flex items-center space-x-2 pb-3 border-b">
            <div
              role="button"
              tabindex="0"
              class="flex items-center space-x-2 cursor-pointer w-full"
              onclick={toggleAllHistory}
              onkeydown={(e) => e.key === 'Enter' && toggleAllHistory()}
            >
              <Checkbox 
                id="select-all" 
                checked={allSelected} 
                onclick={(e: Event) => { e.preventDefault(); }}
              />
              <span class="text-sm font-medium leading-none">
                Select All
              </span>
            </div>
          </div>
          <div class="max-h-[300px] overflow-y-auto space-y-3 pr-2">
            {#each Object.entries(rdpHistory) as [key, value]}
              <div 
                class="flex items-start space-x-2 p-2 hover:bg-muted/50 rounded-md cursor-pointer transition-colors"
                role="button"
                tabindex="0"
                onclick={() => toggleHistoryItem(key)}
                onkeydown={(e) => e.key === 'Enter' && toggleHistoryItem(key)}
              >
                <Checkbox 
                  id={key} 
                  checked={selectedHistoryKeys.includes(key)}
                  onclick={(e: Event) => { e.preventDefault(); }}
                  class="mt-1"
                />
                <div class="grid gap-1.5 leading-none">
                  <span class="text-sm font-medium leading-none">
                    {value}
                  </span>
                  <span class="text-xs text-muted-foreground">
                    Registry Key: {key}
                  </span>
                </div>
              </div>
            {/each}
          </div>
        </div>
      {/if}
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (historyDialogOpen = false)}>
        Cancel
      </Button>
      <Button 
        variant="destructive" 
        disabled={selectedHistoryKeys.length === 0 || deletingHistory}
        loading={deletingHistory}
        onclick={handleDeleteRdpHistory}
      >
        Delete Selected
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
