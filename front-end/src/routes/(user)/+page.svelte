<script lang="ts">
  import { authStore } from "$lib/stores/auth";
  import { createPersistedStore } from "$lib/stores/persisted";
  import { fade } from "svelte/transition";
  import { Icon } from "$lib/components/venUI/icon";
  import {
    AccountsDashboard,
    AdminDashboard,
    ClassicDashboard,
    CustCareDashboard,
    DealerDashboard,
    HoacctDashboard,
    HohrDashboard,
    MgmtDashboard,
    ProdMgmtDashboard,
    SalesDashboard,
    SuperDashboard,
    EcomileProcMgmtDashboard,
    ClaimsDashboard,
    ProcurementDashboard,
    OutstandDashboard,
  } from "$lib/components/venUI/dashboards";
  import DashboardSwitcher from "$lib/components/DashboardSwitcher.svelte";
  import type { DashboardSwitcherOption } from "$lib/components/DashboardSwitcher.svelte";
  import {
    today,
    getLocalTimeZone,
    toCalendarDate,
    parseDate,
    parseDateTime,
    fromDate,
  } from "@internationalized/date";

  const userType = $derived($authStore.user?.userType?.toUpperCase());

  type DashboardMode = "role" | "classic" | "claim" | "procurement" | "outstand";
  const modeStore = createPersistedStore<DashboardMode>(
    "dashboard-mode",
    "role",
  );
  let mode = $state<DashboardMode>(modeStore.get());

  const allDashboardOptions: DashboardSwitcherOption[] = [
    {no: 1, id: "role", label: "My Dashboard", icon: "layout-grid" },
    {no: 2, id: "classic", label: "Classic Sales", icon: "panels-left-bottom" },
    {no: 3, id: "claim", label: "Claims", icon: "file-box" },
    {no: 4, id: "procurement", label: "Procurement", icon: "box" },
    {no: 5, id: "outstand", label: "Outstanding", icon: "wallet" },
  ];

  const allowedDashboards = $derived(
    $authStore.user?.dashboards
      ? $authStore.user.dashboards.split(",").map(d => d.trim()).filter(Boolean)
      : []
  );

  const dashboardOptions = $derived(
    allowedDashboards.length > 0
      ? allDashboardOptions.filter(opt => allowedDashboards.includes(String(opt.no)))
      : allDashboardOptions // Show all options when no explicit user restriction
  );

  $effect(() => {
    // If the persisted mode is no longer allowed, default to the first allowed option
    if (dashboardOptions.length > 0 && !dashboardOptions.find(o => o.id === mode)) {
      mode = dashboardOptions[0].id as DashboardMode;
    }
    modeStore.set(mode);
  });

  /** Work date from auth (same as reportsale). */
  const workDate = $derived($authStore.user?.workDate);
  const ref = $derived.by(() => {
    if (!workDate) return today(getLocalTimeZone());
    try {
      if (typeof workDate === "string") {
        if (workDate.includes("T")) {
          try {
            return toCalendarDate(parseDateTime(workDate.substring(0, 19)));
          } catch {
            return toCalendarDate(
              fromDate(new Date(workDate), getLocalTimeZone()),
            );
          }
        }
        try {
          return parseDate(workDate);
        } catch {
          return toCalendarDate(
            fromDate(new Date(workDate), getLocalTimeZone()),
          );
        }
      }
      return today(getLocalTimeZone());
    } catch {
      return today(getLocalTimeZone());
    }
  });
  const showWorkDateBadge = $derived(
    workDate && ref.toString() !== today(getLocalTimeZone()).toString(),
  );
</script>

<div class="min-h-screen bg-background p-3 pb-24 max-w-7xl mx-auto">
  <!-- ─── Dashboard Switcher ───────────────────────────── -->
  {#if userType !== "ECOPROC"}
    <DashboardSwitcher
      class="mb-2"
      bind:value={mode}
      options={dashboardOptions}
    >
      {#snippet trailing()}
        {#if showWorkDateBadge}
          <div
            class="flex items-center gap-2 rounded-2xl border border-accent/20 bg-accent/10 px-4 py-1.5 text-xs font-black uppercase tracking-widest text-accent-foreground shadow-sm transition-all hover:scale-105"
          >
            <Icon name="calendar-days" class="size-4 text-primary/30" />
            <span class="flex items-center gap-1">
              <span class="opacity-60">Work Date:</span>
              {workDate
                ? new Date(workDate).toLocaleDateString("en-IN", {
                    day: "2-digit",
                    month: "short",
                    year: "numeric",
                  })
                : ""}
            </span>
          </div>
        {/if}
      {/snippet}
    </DashboardSwitcher>
  {/if}
  <!-- ─── Dashboard Content ────────────────────────────── -->
  {#key mode}
    <div in:fade={{ duration: 200 }}>
      {#if mode === "classic"}
        <ClassicDashboard />
      {:else if mode === "role"}
        <div>
          {#if userType === "ACCOUNTS"}
            <AccountsDashboard />
          {:else if userType === "ADMIN"}
            <AdminDashboard />
          {:else if userType === "CUSTCARE" || userType === "CUSTCARE-S"}
            <CustCareDashboard />
          {:else if userType === "DEALER" || userType === "PARTNER"}
            <DealerDashboard />
          {:else if userType === "HOACCT"}
            <HoacctDashboard />
          {:else if userType === "HOHR"}
            <HohrDashboard />
          {:else if userType === "MGMT"}
            <MgmtDashboard />
          {:else if userType === "PRODMGMT"}
            <ProdMgmtDashboard />
          {:else if userType === "SALES"}
            <SalesDashboard />
          {:else if userType === "SUPER"}
            <SuperDashboard />
          {:else if userType === "ECOPROC"}
            <EcomileProcMgmtDashboard />          
          {/if}
        </div>
      {:else if mode === "claim"}
        <ClaimsDashboard />
      {:else if mode === "procurement"}
        <ProcurementDashboard />
      {:else if mode === "outstand"}
        <OutstandDashboard />
      {/if}
    </div>
  {/key}
</div>
