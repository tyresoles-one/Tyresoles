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
  } from "$lib/components/venUI/dashboards";
  import {
    today,
    getLocalTimeZone,
    toCalendarDate,
    parseDate,
    parseDateTime,
    fromDate,
  } from "@internationalized/date";

  const userType = $derived($authStore.user?.userType?.toUpperCase());

  type DashboardMode = "role" | "classic";
  const modeStore = createPersistedStore<DashboardMode>(
    "dashboard-mode",
    "role",
  );
  let mode = $state<DashboardMode>(modeStore.get());

  function setMode(m: DashboardMode) {
    mode = m;
    modeStore.set(m);
  }
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
  <div
    class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-2"
  >
    <div class="ds-bar shadow-xl shadow-primary/5">
      <div class="flex relative">
        <button
          class="ds-option {mode === 'role' ? 'ds-option--active' : ''}"
          onclick={() => setMode("role")}
          aria-pressed={mode === "role"}
        >
          <span class="ds-icon-wrap">
            <Icon name="layout-grid" class="ds-icon" />
          </span>
          <span class="ds-option-text uppercase tracking-tighter"
            >My Dashboard</span
          >
        </button>
        <button
          class="ds-option {mode === 'classic' ? 'ds-option--active' : ''}"
          onclick={() => setMode("classic")}
          aria-pressed={mode === "classic"}
        >
          <span class="ds-icon-wrap">
            <Icon name="panels-left-bottom" class="ds-icon" />
          </span>
          <span class="ds-option-text uppercase tracking-tighter"
            >Classic Sales</span
          >
        </button>
        <span
          class="ds-slider {mode === 'classic'
            ? 'ds-slider--right'
            : ''} bg-gradient-to-br from-primary to-primary/80"
        ></span>
      </div>
    </div>

    {#if showWorkDateBadge}
      <div
        class="flex items-center gap-2 px-4 py-1.5 rounded-2xl bg-accent/10 border border-accent/20 text-accent-foreground text-xs font-black uppercase tracking-widest shadow-sm transition-all hover:scale-105"
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
  </div>
  {/if}
  <!-- ─── Dashboard Content ────────────────────────────── -->
  {#key mode}
    <div in:fade={{ duration: 200 }}>
      {#if mode === "classic"}
        <ClassicDashboard />
      {:else if userType === "DEALER" || userType === "PARTNER"}
        <DealerDashboard />
      {:else if userType === "ACCOUNTS"}
        <AccountsDashboard />
      {:else if userType === "ADMIN"}
        <AdminDashboard />
      {:else if userType === "CUSTCARE" || userType === "CUSTCARE-S"}
        <CustCareDashboard />
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
      {:else}
        <div class="space-y-6">
          <div class="rounded-xl border bg-card p-6 shadow-sm">
            <h2 class="text-2xl font-bold tracking-tight">
              Welcome to VenUI Project
            </h2>
            <p class="text-muted-foreground mt-2">
              Hello {$authStore.user?.fullName || "User"}, no specific dashboard
              is assigned for your user type: {userType || "Unknown"}.
            </p>
          </div>
        </div>
      {/if}
    </div>
  {/key}
</div>

<style>
  /* ── Switcher Bar ──────────────────────────────────── */
  .ds-bar {
    position: relative;
    display: inline-flex;
    align-items: stretch;
    border-radius: 999px;
    border: 1px solid var(--border);
    background: var(--muted);
    padding: 0.1875rem;
    gap: 0;
    margin-bottom: 0.5rem;
    box-shadow: 0 1px 3px color-mix(in oklch, var(--foreground) 4%, transparent);
  }

  /* ── Option Button ─────────────────────────────────── */
  .ds-option {
    position: relative;
    z-index: 1;
    display: inline-flex;
    align-items: center;
    gap: 0.375rem;
    padding: 0.375rem 0.75rem;
    border: none;
    border-radius: 999px;
    background: transparent;
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--muted-foreground);
    cursor: pointer;
    white-space: nowrap;
    transition:
      color 0.25s ease,
      transform 0.1s ease;
    -webkit-tap-highlight-color: transparent;
    user-select: none;
  }

  .ds-option:active {
    transform: scale(0.96);
  }

  .ds-option--active {
    color: var(--primary-foreground);
  }

  /* ── Icon ───────────────────────────────────────────── */
  .ds-icon-wrap {
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  :global(.ds-icon) {
    width: 0.8rem;
    height: 0.8rem;
    color: inherit;
  }

  .ds-option-text {
    display: none;
  }

  @media (min-width: 360px) {
    .ds-option-text {
      display: inline;
    }
  }

  /* ── Sliding Indicator ─────────────────────────────── */
  .ds-slider {
    position: absolute;
    top: 0.1875rem;
    left: 0.1875rem;
    width: calc(50% - 0.1875rem);
    height: calc(100% - 0.375rem);
    border-radius: 999px;
    background: var(--primary);
    box-shadow:
      0 1px 4px color-mix(in oklch, var(--primary) 30%, transparent),
      inset 0 1px 0 color-mix(in oklch, white 12%, transparent);
    transition:
      transform 0.3s cubic-bezier(0.4, 0, 0.2, 1),
      box-shadow 0.3s ease;
    pointer-events: none;
  }

  .ds-slider--right {
    transform: translateX(100%);
  }

  /* ── Hover refinement (non-touch) ──────────────────── */
  @media (hover: hover) {
    .ds-option:not(.ds-option--active):hover {
      color: var(--foreground);
    }

    .ds-bar:hover .ds-slider {
      box-shadow:
        0 2px 8px color-mix(in oklch, var(--primary) 40%, transparent),
        inset 0 1px 0 color-mix(in oklch, white 12%, transparent);
    }
  }

  /* ── Larger touch target on mobile ─────────────────── */
  @media (max-width: 359px) {
    .ds-option {
      padding: 0.4375rem 0.75rem;
    }
  }

  @media (min-width: 480px) {
    .ds-option {
      padding: 0.375rem 1rem;
      font-size: 0.75rem;
    }

    :global(.ds-icon) {
      width: 0.875rem;
      height: 0.875rem;
    }
  }
</style>
