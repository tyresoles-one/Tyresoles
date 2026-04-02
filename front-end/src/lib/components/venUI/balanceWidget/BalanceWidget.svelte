<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import {
    GetMyBalanceDocument,
    type GetMyBalanceQuery,
    GetMyTransactionsDocument,
    type GetMyTransactionsQuery,
  } from "$lib/services/graphql/generated/types.js";
  import { Icon } from "$lib/components/venUI/icon";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { fade, slide } from "svelte/transition";

  type BalanceItem = GetMyBalanceQuery["myBalance"][number];
  type TxItem = NonNullable<
    NonNullable<GetMyTransactionsQuery["myTransactions"]>["nodes"]
  >[number];

  let {
    entityType = null,
    entityCode = null,
    respCenter = null,
    class: className = "",
  }: {
    entityType?: string | null;
    entityCode?: string | null;
    respCenter?: string | null;
    class?: string;
  } = $props();

  // ── Balance ──────────────────────────────────────────
  let loadingBal = $state(true);
  let errorBal = $state<string | null>(null);
  let balances = $state<BalanceItem[]>([]);
  let expandedAccounts = $state(false);

  async function fetchBalance() {
    loadingBal = true;
    errorBal = null;
    try {
      const res = await graphqlQuery<GetMyBalanceQuery>(GetMyBalanceDocument, {
        variables: { entityType, entityCode, respCenter },
        skipCache: true,
      });
      if (res.success && res.data?.myBalance) {
        balances = res.data.myBalance;
      } else {
        errorBal = res.error || "Failed to fetch balance.";
      }
    } catch {
      errorBal = "An unexpected error occurred.";
    } finally {
      loadingBal = false;
    }
  }

  // ── Transactions ─────────────────────────────────────
  let showTx = $state(false);
  let txFetched = $state(false);   // lazy: only fetch once
  let loadingTx = $state(false);
  let errorTx = $state<string | null>(null);
  let transactions = $state<TxItem[]>([]);

  async function fetchTransactions() {
    loadingTx = true;
    errorTx = null;
    try {
      const res = await graphqlQuery<GetMyTransactionsQuery>(
        GetMyTransactionsDocument,
        {
          variables: { entityType, entityCode, respCenter },
          skipCache: true,
        }
      );
      if (res.success && res.data?.myTransactions?.nodes) {
        transactions = res.data.myTransactions.nodes;
      } else {
        errorTx = res.error || "Failed to fetch transactions.";
      }
    } catch {
      errorTx = "An unexpected error occurred.";
    } finally {
      loadingTx = false;
    }
  }

  async function toggleTx() {
    showTx = !showTx;
    if (showTx && !txFetched) {
      txFetched = true;
      await fetchTransactions();
    }
  }

  onMount(() => {
    fetchBalance();
  });

  function toNum(v: unknown): number {
    return typeof v === "number" ? v : parseFloat(String(v)) || 0;
  }

  let totalBalance = $derived(
    balances.reduce((acc, b) => acc + toNum(b?.balance), 0)
  );

  function fmt(n: number) {
    return n.toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  }

  let isPositive = $derived(totalBalance >= 0);

  // ── Helpers ───────────────────────────────────────────
  /** Document type int → label */
  function txTypeLabel(type: number): string {
    const map: Record<number, string> = {
      0: "Credit Memo",
      1: "Payment",
      2: "Invoice",
      3: "Reminder",
      4: "Finance Charge",
    };
    return map[type] ?? `Type ${type}`;
  }

  function fmtDate(iso: string | null): string {
    if (!iso) return "—";
    return new Date(iso).toLocaleDateString("en-IN", {
      day: "2-digit",
      month: "short",
    });
  }

  async function refreshAll() {
    await Promise.all([
      fetchBalance(),
      ...(txFetched ? [fetchTransactions()] : []),
    ]);
  }
</script>

<div class="bw-tile {className}" role="region" aria-label="Account Balance">

  <!-- ── Header ───────────────────────────────────── -->
  <div class="bw-header">
    <div class="bw-icon-wrap">
      <Icon name="landmark" class="bw-icon" />
    </div>
    <div class="bw-title-col">
      <span class="bw-label">Account Balance</span>
      {#if entityCode}
        <span class="bw-sub">{entityCode}</span>
      {/if}
    </div>
    <button
      class="bw-refresh"
      onclick={refreshAll}
      disabled={loadingBal || loadingTx}
      title="Refresh"
      aria-label="Refresh"
    >
      <Icon
        name="refresh-cw"
        class="bw-refresh-icon {loadingBal || loadingTx ? 'spinning' : ''}"
      />
    </button>
  </div>

  <div class="bw-divider"></div>

  <!-- ── Balance Amount ────────────────────────────── -->
  <div class="bw-amount-wrap">
    {#if loadingBal}
      <div class="bw-skel-group" in:fade={{ duration: 150 }}>
        <Skeleton class="bw-skel-lg" />
        <Skeleton class="bw-skel-sm" />
      </div>
    {:else if errorBal}
      <div class="bw-error" in:slide={{ duration: 200 }}>
        <Icon name="alert-circle" class="bw-error-icon" />
        <span>{errorBal}</span>
      </div>
    {:else}
      <div in:fade={{ duration: 250 }}>
        <p class="bw-amount">₹{fmt(totalBalance)}</p>
        <p class="bw-amount-sub">Outstanding · Ledger</p>
      </div>
    {/if}
  </div>

  <!-- ── Status / accounts footer ─────────────────── -->
  {#if !loadingBal && !errorBal}
    <div class="bw-footer" in:fade={{ duration: 200, delay: 80 }}>
      <span class="bw-status {!isPositive ? 'bw-credit' : 'bw-debit'}">
        <span class="bw-dot"></span>
        {!isPositive ? "Credit" : "Debit"}
      </span>

      {#if balances.length > 1}
        <button
          class="bw-expand"
          onclick={() => (expandedAccounts = !expandedAccounts)}
          aria-expanded={expandedAccounts}
        >
          {expandedAccounts ? "Hide" : `${balances.length} accounts`}
          <Icon
            name={expandedAccounts ? "chevron-up" : "chevron-down"}
            class="bw-chevron"
          />
        </button>
      {:else if balances.length === 1}
        <span class="bw-single-acct">
          <Icon name="building-2" class="bw-acct-icon" />
          {balances[0]?.code || "Account"}
        </span>
      {/if}
    </div>
  {/if}

  <!-- ── Multi-account breakdown ───────────────────── -->
  {#if !loadingBal && !errorBal && expandedAccounts && balances.length > 1}
    <div
      class="bw-breakdown"
      in:slide={{ duration: 220 }}
      out:slide={{ duration: 180 }}
    >
      {#each balances as balance, i}
        <div class="bw-row" in:fade={{ duration: 180, delay: i * 35 }}>
          <div class="bw-avatar">
            {(balance?.product || "?").charAt(0).toUpperCase()}
          </div>
          <span class="bw-row-code">{balance?.code || "Unknown"}</span>
          <span class="bw-row-amount">₹{fmt(toNum(balance?.balance))}</span>
        </div>
      {/each}
    </div>
  {/if}

  <!-- ── Empty balance state ───────────────────────── -->
  {#if !loadingBal && !errorBal && balances.length === 0}
    <div class="bw-empty" in:fade>
      <Icon name="inbox" class="bw-empty-icon" />
      <p>No balance data</p>
    </div>
  {/if}

  <!-- ── Recent Transactions ───────────────────────── -->
  <div class="bw-tx-section">
    <button class="bw-tx-header" onclick={toggleTx} aria-expanded={showTx}>
      <Icon name="clock" class="bw-tx-clock" />
      <span class="bw-tx-title">Recent Transactions</span>
      <Icon name={showTx ? 'chevron-up' : 'chevron-down'} class="bw-tx-chevron" />
    </button>

    {#if showTx}
      <div in:slide={{ duration: 200 }} out:slide={{ duration: 160 }}>
        {#if loadingTx}
          <div class="bw-tx-skel-group" in:fade={{ duration: 150 }}>
            {#each { length: 3 } as _}
              <div class="bw-tx-skel-row">
                <Skeleton class="bw-skel-tx-doc" />
                <Skeleton class="bw-skel-tx-amt" />
              </div>
            {/each}
          </div>
        {:else if errorTx}
          <div class="bw-tx-error" in:slide={{ duration: 200 }}>
            <Icon name="alert-circle" class="bw-error-icon" />
            <span>{errorTx}</span>
          </div>
        {:else if transactions.length === 0}
          <div class="bw-tx-empty" in:fade>
            <Icon name="inbox" class="bw-empty-icon" />
            <p>No recent transactions</p>
          </div>
        {:else}
          <div class="bw-tx-list" in:fade={{ duration: 250 }}>
            {#each transactions as tx, i}
              <div class="bw-tx-row" in:fade={{ duration: 180, delay: i * 40 }}>
                <!-- Doc type badge -->
                <div class="bw-tx-badge">
                  <Icon name="file-text" class="bw-tx-badge-icon" />
                </div>
                <!-- Doc info -->
                <div class="bw-tx-info">
                  <span class="bw-tx-doc">{tx.documentNo || "—"}</span>
                  <span class="bw-tx-meta">{txTypeLabel(tx.type)} · {fmtDate(tx.date)}</span>
                  {#if tx.customerNo}
                    <span class="bw-tx-cust">{tx.customerNo}</span>
                  {/if}
                </div>
                <!-- Amount -->
                <span
                  class="bw-tx-amount {toNum(tx.amount) < 0
                    ? 'bw-tx-credit'
                    : toNum(tx.amount) > 0
                      ? 'bw-tx-debit'
                      : 'bw-tx-zero'}"
                >
                  {toNum(tx.amount) < 0 ? "" : toNum(tx.amount) > 0 ? "+" : ""}₹{fmt(
                    Math.abs(toNum(tx.amount))
                  )}
                </span>
              </div>
            {/each}
          </div>
        {/if}

        <!-- Show More footer -->
        {#if !loadingTx && !errorTx}
          <div class="bw-tx-footer">
            <button
              class="bw-show-more"
              onclick={() => goto('/my-transactions')}
            >
              Show all transactions
              <Icon name="arrow-right" class="bw-show-more-arrow" />
            </button>
          </div>
        {/if}
      </div>
    {/if}
  </div>
</div>

<style>
  /* ── Shell ───────────────────────────────────────────── */
  .bw-tile {
    position: relative;
    border-radius: var(--radius-lg, 0.625rem);
    border: 1px solid var(--border);
    background: var(--card);
    color: var(--card-foreground);
    padding: 0.875rem 1rem 0.875rem;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    transition: box-shadow 0.2s ease, border-color 0.2s ease;
    min-width: 0;
  }

  .bw-tile:hover {
    border-color: var(--ring);
    box-shadow: 0 2px 12px color-mix(in oklch, var(--primary) 10%, transparent);
  }

  /* ── Header ──────────────────────────────────────────── */
  .bw-header {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  .bw-icon-wrap {
    flex-shrink: 0;
    width: 1.875rem;
    height: 1.875rem;
    border-radius: calc(var(--radius-lg) - 2px);
    background: var(--muted);
    display: flex;
    align-items: center;
    justify-content: center;
  }

  :global(.bw-icon) {
    width: 0.9rem;
    height: 0.9rem;
    color: var(--muted-foreground);
  }

  .bw-title-col {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
  }

  .bw-label {
    font-size: 0.625rem;
    font-weight: 700;
    letter-spacing: 0.09em;
    text-transform: uppercase;
    color: var(--muted-foreground);
    line-height: 1;
  }

  .bw-sub {
    font-size: 0.65rem;
    color: var(--muted-foreground);
    margin-top: 0.15rem;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* ── Refresh ─────────────────────────────────────────── */
  .bw-refresh {
    flex-shrink: 0;
    width: 1.625rem;
    height: 1.625rem;
    border-radius: 50%;
    border: 1px solid var(--border);
    background: transparent;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    color: var(--muted-foreground);
    transition: background 0.15s ease, color 0.15s ease, border-color 0.15s ease;
  }

  .bw-refresh:hover {
    background: var(--muted);
    color: var(--foreground);
    border-color: var(--ring);
  }

  .bw-refresh:active { transform: scale(0.9); }
  .bw-refresh:disabled { opacity: 0.4; cursor: not-allowed; }

  :global(.bw-refresh-icon) {
    width: 0.725rem;
    height: 0.725rem;
    color: inherit;
  }

  :global(.spinning) {
    animation: spin 0.9s linear infinite;
  }

  @keyframes spin { to { transform: rotate(360deg); } }

  /* ── Divider ─────────────────────────────────────────── */
  .bw-divider {
    height: 1px;
    background: var(--border);
    margin: 0 -0.25rem;
  }

  /* ── Amount ──────────────────────────────────────────── */
  .bw-amount-wrap {
    min-height: 2.5rem;
  }

  .bw-amount {
    font-size: 1.5rem;
    font-weight: 800;
    letter-spacing: -0.03em;
    color: var(--foreground);
    line-height: 1.1;
  }

  .bw-amount-sub {
    font-size: 0.575rem;
    font-weight: 600;
    letter-spacing: 0.07em;
    text-transform: uppercase;
    color: var(--muted-foreground);
    margin-top: 0.2rem;
    opacity: 0.7;
  }

  /* ── Skeleton ────────────────────────────────────────── */
  .bw-skel-group {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    padding-top: 0.2rem;
  }

  :global(.bw-skel-lg) {
    height: 1.5rem;
    width: 7.5rem;
    border-radius: 0.375rem;
  }

  :global(.bw-skel-sm) {
    height: 0.55rem;
    width: 4rem;
    border-radius: 0.25rem;
  }

  /* ── Error ───────────────────────────────────────────── */
  .bw-error,
  .bw-tx-error {
    display: flex;
    align-items: flex-start;
    gap: 0.35rem;
    background: color-mix(in oklch, var(--destructive) 12%, transparent);
    border: 1px solid color-mix(in oklch, var(--destructive) 25%, transparent);
    border-radius: calc(var(--radius-lg) - 2px);
    padding: 0.4rem 0.55rem;
    font-size: 0.7rem;
    color: var(--destructive);
  }

  :global(.bw-error-icon) {
    width: 0.8rem;
    height: 0.8rem;
    flex-shrink: 0;
    margin-top: 0.05rem;
    color: var(--destructive);
  }

  /* ── Footer ──────────────────────────────────────────── */
  .bw-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
  }

  .bw-status {
    display: inline-flex;
    align-items: center;
    gap: 0.3rem;
    font-size: 0.625rem;
    font-weight: 600;
    color: var(--muted-foreground);
  }

  .bw-dot {
    width: 0.375rem;
    height: 0.375rem;
    border-radius: 50%;
    display: inline-block;
    flex-shrink: 0;
  }

  .bw-credit .bw-dot { background: oklch(0.62 0.17 160); }
  .bw-debit  .bw-dot { background: oklch(0.62 0.22 25);  }

  .bw-expand {
    display: inline-flex;
    align-items: center;
    gap: 0.2rem;
    background: none;
    border: none;
    cursor: pointer;
    font-size: 0.625rem;
    font-weight: 600;
    color: var(--muted-foreground);
    padding: 0.15rem 0.35rem;
    border-radius: calc(var(--radius-lg) - 4px);
    transition: color 0.15s ease, background 0.15s ease;
  }

  .bw-expand:hover {
    color: var(--foreground);
    background: var(--muted);
  }

  :global(.bw-chevron) {
    width: 0.6rem;
    height: 0.6rem;
    color: inherit;
  }

  .bw-single-acct {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    font-size: 0.625rem;
    color: var(--muted-foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 6.5rem;
    opacity: 0.8;
  }

  :global(.bw-acct-icon) {
    width: 0.625rem;
    height: 0.625rem;
    flex-shrink: 0;
    color: var(--muted-foreground);
  }

  /* ── Multi-account breakdown ─────────────────────────── */
  .bw-breakdown {
    border-top: 1px solid var(--border);
    padding-top: 0.4rem;
    display: flex;
    flex-direction: column;
  }

  .bw-row {
    display: flex;
    align-items: center;
    gap: 0.45rem;
    padding: 0.3rem 0;
    border-bottom: 1px solid var(--border);
  }

  .bw-row:last-child { border-bottom: none; }

  .bw-avatar {
    flex-shrink: 0;
    width: 1.375rem;
    height: 1.375rem;
    border-radius: calc(var(--radius-lg) - 4px);
    background: var(--muted);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.6rem;
    font-weight: 700;
    color: var(--muted-foreground);
  }

  .bw-row-code {
    flex: 1;
    font-size: 0.7rem;
    font-weight: 500;
    color: var(--foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .bw-row-amount {
    font-size: 0.7rem;
    font-weight: 700;
    color: var(--foreground);
    flex-shrink: 0;
  }

  /* ── Empty ───────────────────────────────────────────── */
  .bw-empty,
  .bw-tx-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.3rem;
    padding: 0.25rem 0;
    opacity: 0.5;
  }

  :global(.bw-empty-icon) {
    width: 1.25rem;
    height: 1.25rem;
    color: var(--muted-foreground);
  }

  .bw-empty p,
  .bw-tx-empty p {
    font-size: 0.65rem;
    color: var(--muted-foreground);
  }

  /* ── Transactions section ────────────────────────────── */
  .bw-tx-section {
    border-top: 1px solid var(--border);
    padding-top: 0.5rem;
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

  .bw-tx-header {
    display: flex;
    align-items: center;
    gap: 0.3rem;
    width: 100%;
    background: none;
    border: none;
    border-radius: calc(var(--radius-lg) - 4px);
    padding: 0.2rem 0.3rem;
    margin: 0 -0.3rem;
    cursor: pointer;
    color: inherit;
    transition: background 0.15s ease;
  }

  .bw-tx-header:hover  { background: var(--muted); }
  .bw-tx-header:active { opacity: 0.7; }

  :global(.bw-tx-chevron) {
    width: 0.65rem;
    height: 0.65rem;
    color: var(--muted-foreground);
    margin-left: auto;
  }

  :global(.bw-tx-clock) {
    width: 0.65rem;
    height: 0.65rem;
    color: var(--muted-foreground);
  }

  .bw-tx-title {
    font-size: 0.575rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--muted-foreground);
  }

  /* ── Tx skeleton ─────────────────────────────────────── */
  .bw-tx-skel-group {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
  }

  .bw-tx-skel-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
  }

  :global(.bw-skel-tx-doc) {
    height: 0.65rem;
    width: 5.5rem;
    border-radius: 0.25rem;
  }

  :global(.bw-skel-tx-amt) {
    height: 0.65rem;
    width: 3.5rem;
    border-radius: 0.25rem;
  }

  /* ── Tx list ─────────────────────────────────────────── */
  .bw-tx-list {
    display: flex;
    flex-direction: column;
    gap: 0;
  }

  .bw-tx-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.3rem 0;
    border-bottom: 1px solid var(--border);
  }

  .bw-tx-row:last-child { border-bottom: none; }

  .bw-tx-badge {
    flex-shrink: 0;
    width: 1.5rem;
    height: 1.5rem;
    border-radius: calc(var(--radius-lg) - 4px);
    background: var(--muted);
    display: flex;
    align-items: center;
    justify-content: center;
  }

  :global(.bw-tx-badge-icon) {
    width: 0.65rem;
    height: 0.65rem;
    color: var(--muted-foreground);
  }

  .bw-tx-info {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
  }

  .bw-tx-doc {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .bw-tx-meta {
    font-size: 0.575rem;
    color: var(--muted-foreground);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .bw-tx-cust {
    font-size: 0.6rem;
    font-weight: 600;
    color: var(--foreground);
    opacity: 0.6;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .bw-tx-amount {
    font-size: 0.7rem;
    font-weight: 700;
    flex-shrink: 0;
    text-align: right;
  }

  .bw-tx-credit { color: oklch(0.52 0.16 160); }   /* < 0  payment in  → green */
  .bw-tx-debit  { color: oklch(0.52 0.20 25);  }   /* > 0  charge/inv  → red   */
  .bw-tx-zero   { color: var(--muted-foreground); } /* = 0  no movement → muted */

  .bw-tx-footer {
    display: flex;
    justify-content: flex-end;
    padding-top: 0.45rem;
    margin-top: 0.1rem;
    border-top: 1px solid var(--border);
  }

  .bw-show-more {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    font-size: 0.625rem;
    font-weight: 700;
    letter-spacing: 0.04em;
    color: var(--primary);
    background: none;
    border: none;
    padding: 0;
    cursor: pointer;
    transition: opacity 0.15s;
  }

  .bw-show-more:hover { opacity: 0.75; }

  :global(.bw-show-more-arrow) {
    width: 0.6rem;
    height: 0.6rem;
    color: inherit;
  }
</style>
