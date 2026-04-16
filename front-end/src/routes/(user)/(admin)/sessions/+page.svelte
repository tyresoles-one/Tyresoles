<script lang="ts">
  import { onMount } from "svelte";
  import { fade, slide } from "svelte/transition";
  import { graphqlQuery, graphqlMutation } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Toast } from "$lib/components/venUI/toast";
  import { Dialog } from "$lib/components/venUI/dialog";
  import { formatDistanceToNow } from "date-fns";
  import { cn } from "$lib/utils";

  // ── Types ──────────────────────────────────────────────────
  interface SessionInfo {
    sessionId: string;
    userId: string;
    userType: string;
    entityType: string;
    entityCode: string;
    department: string;
    createdAtUtc: string;
    expiresAtUtc: string;
  }

  const GET_SESSIONS = `
    query GetSessions($userId: String) {
      sessions(userId: $userId) {
        sessionId
        userId
        userType
        entityType
        entityCode
        department
        createdAtUtc
        expiresAtUtc
      }
    }
  `;

  const KILL_SESSION = `
    mutation KillSession($sessionId: String!) {
      killSession(sessionId: $sessionId) {
        success
        message
      }
    }
  `;

  const KILL_SESSIONS_BY_USER = `
    mutation KillSessionsByUser($userId: String!) {
      killSessionsByUser(userId: $userId) {
        success
        killedCount
      }
    }
  `;

  // ── State ──────────────────────────────────────────────────
  let sessions = $state<SessionInfo[]>([]);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let searchTerm = $state("");
  let selectedId = $state<string | null>(null);
  let actionLoading = $state<string | null>(null);

  // ── Data Fetching ──────────────────────────────────────────
  async function fetchSessions() {
    loading = true;
    error = null;
    try {
      const res = await graphqlQuery<{ sessions: SessionInfo[] }>(GET_SESSIONS, {
        skipCache: true,
      });
      if (res.success && res.data) {
        sessions = res.data.sessions;
      } else {
        error = res.error || "Failed to load sessions.";
      }
    } catch {
      error = "An unexpected error occurred.";
    } finally {
      loading = false;
    }
  }

  async function killSession(id: string) {
    const confirmed = await Dialog.confirm(
      "End this session?",
      "They will be signed out immediately on their next request (or right away if the app is open).",
      {
        confirmLabel: "End session",
        cancelLabel: "Cancel",
        variant: "destructive",
        icon: "power",
      },
    );
    if (!confirmed) return;

    actionLoading = id;
    try {
      const res = await graphqlMutation<{ killSession: { success: boolean, message: string } }>(KILL_SESSION, {
        variables: { sessionId: id }
      });
      if (res.success && res.data?.killSession.success) {
        Toast.success("Session ended", "That user is signed out immediately.");
        sessions = sessions.filter(s => s.sessionId !== id);
        if (selectedId === id) selectedId = null;
      } else {
        Toast.error(res.data?.killSession.message || "Failed to end session");
      }
    } catch {
      Toast.error("Request failed");
    } finally {
      actionLoading = null;
    }
  }

  async function killAllForUser(userId: string) {
    const confirmed = await Dialog.confirm(
      `End all sessions for ${userId}?`,
      "Every active session for this user ends immediately on their next request.",
      {
        confirmLabel: "End all sessions",
        cancelLabel: "Cancel",
        variant: "destructive",
        icon: "users-round",
      },
    );
    if (!confirmed) return;

    actionLoading = `user-${userId}`;
    try {
      const res = await graphqlMutation<{ killSessionsByUser: { success: boolean, killedCount: number } }>(KILL_SESSIONS_BY_USER, {
        variables: { userId }
      });
      if (res.success && res.data?.killSessionsByUser.success) {
        Toast.success(
          "Sessions ended",
          `${res.data.killSessionsByUser.killedCount} session(s) cleared — those clients are signed out.`,
        );
        sessions = sessions.filter(s => s.userId !== userId);
      } else {
        Toast.error("Failed to end user sessions");
      }
    } catch {
      Toast.error("Request failed");
    } finally {
      actionLoading = null;
    }
  }

  // ── Helpers ────────────────────────────────────────────────
  let filteredSessions = $derived(
    sessions
      .filter(s => isLiveSession(s.expiresAtUtc))
      .filter(
        s =>
          s.userId.toLowerCase().includes(searchTerm.toLowerCase()) ||
          s.entityCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
          s.department.toLowerCase().includes(searchTerm.toLowerCase()),
      ),
  );

  /** GraphQL often returns ISO strings that already end with `Z`; appending `Z` again yields Invalid Date. */
  function parseUtcDate(iso: string): Date {
    const s = (iso ?? "").trim();
    if (!s) return new Date(NaN);
    if (/Z$/i.test(s)) return new Date(s);
    if (/[+-]\d{2}:?\d{2}$/.test(s)) return new Date(s);
    return new Date(`${s}Z`);
  }

  function formatDate(iso: string) {
    try {
      return formatDistanceToNow(parseUtcDate(iso), { addSuffix: true });
    } catch {
      return iso;
    }
  }

  function getStatusColor(expiresAt: string) {
    const isExpired = parseUtcDate(expiresAt) <= new Date();
    return isExpired ? "text-rose-500" : "text-emerald-500";
  }

  /** Server should only return active rows; keep this so the UI never lists expired tokens as "live". */
  function isLiveSession(expiresAtUtc: string): boolean {
    return parseUtcDate(expiresAtUtc) > new Date();
  }

  onMount(() => {
    fetchSessions();
  });
</script>

<svelte:head>
  <title>Session Management - Admin</title>
</svelte:head>

<div class="session-page">
  <PageHeading backHref="/" icon="shield-check">
    {#snippet title()}Session Management{/snippet}
  </PageHeading>

  <!-- ── Toolbar ───────────────────────────────────────────── -->
  <div class="session-toolbar">
    <div class="session-summary-chip">
      {#if loading}
        <span class="session-chip-label">Fetching live sessions…</span>
      {:else}
        <span class="session-chip-label">{filteredSessions.length} live sessions</span>
      {/if}
    </div>

    <div class="session-toolbar-right">
      <div class="session-search-wrap">
        <Icon name="search" class="session-search-icon" />
        <input 
          class="session-search-input" 
          type="text" 
          placeholder="Filter by User, Dept or Code..." 
          bind:value={searchTerm} 
        />
      </div>

      <button class="session-icon-btn" onclick={fetchSessions} disabled={loading}>
        <Icon name="refresh-cw" class="session-toolbar-icon {loading ? 'spinning' : ''}" />
      </button>
    </div>
  </div>

  <!-- ── Main Content ──────────────────────────────────────── -->
  <div class="session-main">
    <div class="session-list-wrap">
      {#if loading}
        <div class="space-y-4">
          {#each { length: 5 } as _}
            <div class="h-20 w-full animate-pulse rounded-xl bg-muted/40"></div>
          {/each}
        </div>
      {:else if error}
        <div class="flex flex-col items-center justify-center py-20 text-center">
          <div class="bg-rose-500/10 p-4 rounded-full mb-4">
             <Icon name="alert-triangle" class="h-10 w-10 text-rose-500" />
          </div>
          <p class="text-sm font-semibold text-rose-500">{error}</p>
          <button class="mt-4 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-xs font-bold" onclick={fetchSessions}>
            Retry Connection
          </button>
        </div>
      {:else if filteredSessions.length === 0}
        <div class="flex flex-col items-center justify-center py-20 text-center opacity-40">
          <Icon name="monitor-off" class="h-16 w-16 mb-4" />
          <p class="text-lg font-medium">No live sessions found</p>
          <p class="text-xs">Adjust your filters or refresh the list</p>
        </div>
      {:else}
        <div class="session-list">
          {#each filteredSessions as s (s.sessionId)}
            <div class="session-card-group">
                <div
                role="button"
                tabindex="0"
                class="session-row {selectedId === s.sessionId ? 'session-row--selected' : ''}"
                onclick={() => selectedId = selectedId === s.sessionId ? null : s.sessionId}
                onkeydown={(e) => e.key === 'Enter' && (selectedId = selectedId === s.sessionId ? null : s.sessionId)}
                >
                <div class="session-row-icon">
                    <Icon name={s.userType === 'Employee' ? 'user' : 'store'} class="w-5 h-5" />
                </div>
                
                <div class="session-row-info">
                    <span class="session-row-userId">{s.userId}</span>
                    <div class="session-row-meta">
                        <span class="session-row-type">{s.userType}</span>
                        <span class="session-bullet"></span>
                        <span class="session-row-dept">{s.department}</span>
                    </div>
                </div>

                <div class="session-row-timing">
                    <span class="session-row-label">Logged in</span>
                    <span class="session-row-value">{formatDate(s.createdAtUtc)}</span>
                </div>

                <div class="session-row-actions">
                    <button 
                        class="session-kill-btn" 
                        onclick={(e) => { e.stopPropagation(); killSession(s.sessionId); }}
                        disabled={actionLoading === s.sessionId}
                    >
                        {#if actionLoading === s.sessionId}
                            <Icon name="loader-2" class="w-4 h-4 animate-spin" />
                        {:else}
                            <Icon name="power" class="w-4 h-4" />
                        {/if}
                    </button>
                </div>
                </div>

                {#if selectedId === s.sessionId}
                <div class="session-details" in:slide={{ duration: 250 }}>
                    <div class="session-details-grid">
                        <div class="session-detail-item">
                            <span class="session-detail-label">Session ID</span>
                            <span class="session-detail-value font-mono text-[0.6rem] truncate">{s.sessionId}</span>
                        </div>
                        <div class="session-detail-item">
                            <span class="session-detail-label">Entity Code</span>
                            <span class="session-detail-value">{s.entityCode} ({s.entityType})</span>
                        </div>
                        <div class="session-detail-item">
                            <span class="session-detail-label">Expires</span>
                            <span class="session-detail-value {getStatusColor(s.expiresAtUtc)}">
                                {parseUtcDate(s.expiresAtUtc).toLocaleString()}
                            </span>
                        </div>
                        <div class="session-detail-item flex items-end justify-end">
                            <button 
                                class="kill-user-btn"
                                onclick={() => killAllForUser(s.userId)}
                                disabled={actionLoading === `user-${s.userId}`}
                            >
                                <Icon name="users-round" class="w-3.5 h-3.5 mr-1.5" />
                                Kill all for this user
                            </button>
                        </div>
                    </div>
                </div>
                {/if}
            </div>
          {/each}
        </div>
      {/if}
    </div>
  </div>
</div>

<style>
  .session-page {
    min-height: 100svh;
    background: var(--background);
    color: var(--foreground);
    display: flex;
    flex-direction: column;
  }

  .session-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1.5rem;
    border-bottom: 1px solid var(--border);
    background: var(--card);
    position: sticky;
    top: 0;
    z-index: 20;
    gap: 1rem;
  }

  .session-chip-label {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--muted-foreground);
    white-space: nowrap;
  }

  .session-toolbar-right {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    flex: 1;
    justify-content: flex-end;
  }

  .session-search-wrap {
    position: relative;
    max-width: 300px;
    width: 100%;
  }

  :global(.session-search-icon) {
    position: absolute;
    left: 0.75rem;
    top: 50%;
    transform: translateY(-50%);
    width: 0.9rem;
    height: 0.9rem;
    color: var(--muted-foreground);
    opacity: 0.5;
  }

  .session-search-input {
    width: 100%;
    padding: 0.45rem 1rem 0.45rem 2.25rem;
    font-size: 0.75rem;
    border: 1px solid var(--border);
    border-radius: 999px;
    background: var(--background);
    outline: none;
    transition: all 0.2s;
  }

  .session-search-input:focus {
    border-color: var(--primary);
    box-shadow: 0 0 0 2px var(--primary)/10;
  }

  .session-icon-btn {
    width: 2.25rem;
    height: 2.25rem;
    border-radius: 50%;
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--muted-foreground);
    transition: all 0.2s;
    background: var(--card);
  }

  .session-icon-btn:hover { background: var(--muted); color: var(--foreground); }

  .session-main {
    flex: 1;
    overflow-y: auto;
    padding: 1.5rem;
  }

  .session-list-wrap {
    max-width: 800px;
    margin: 0 auto;
    width: 100%;
  }

  .session-list {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .session-card-group {
    display: flex;
    flex-direction: column;
  }

  .session-row {
    display: flex;
    align-items: center;
    gap: 1rem;
    padding: 1rem 1.25rem;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 1rem;
    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
    text-align: left;
    width: 100%;
    cursor: pointer;
  }

  .session-row:hover { 
    border-color: var(--ring); 
    transform: scale(1.01);
    box-shadow: 0 10px 15px -3px rgba(0,0,0,0.05);
  }

  .session-row--selected { 
    border-color: var(--primary); 
    background: var(--primary)/5; 
    border-bottom-left-radius: 0; 
    border-bottom-right-radius: 0;
  }

  .session-row-icon {
    width: 2.5rem;
    height: 2.5rem;
    border-radius: 0.75rem;
    background: var(--muted);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--primary);
  }

  .session-row-info {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .session-row-userId {
    font-size: 0.9375rem;
    font-weight: 600;
    color: var(--foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .session-row-meta {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.7rem;
    color: var(--muted-foreground);
    font-weight: 500;
  }

  .session-bullet {
    width: 3px;
    height: 3px;
    border-radius: 50%;
    background: var(--muted-foreground);
    opacity: 0.5;
  }

  .session-row-timing {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    font-size: 0.7rem;
    margin-right: 1rem;
  }

  .session-row-label { color: var(--muted-foreground); font-weight: 500; }
  .session-row-value { font-weight: 600; color: var(--foreground); }

  .session-kill-btn {
    width: 2rem;
    height: 2rem;
    border-radius: 0.5rem;
    background: color-mix(in oklch, var(--destructive) 14%, transparent);
    color: var(--destructive);
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
  }

  .session-kill-btn:hover:not(:disabled) {
    background: var(--destructive);
    color: var(--primary-foreground);
  }

  .session-kill-btn:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .session-details {
    background: var(--card);
    border: 1px solid var(--primary);
    border-top: none;
    border-bottom-left-radius: 1rem;
    border-bottom-right-radius: 1rem;
    padding: 1.25rem;
    margin-top: -1px;
    box-shadow: inset 0 2px 10px rgba(0,0,0,0.03);
  }

  .session-details-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: 1.5rem;
  }

  .session-detail-item {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

  .session-detail-label {
    font-size: 0.6rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--muted-foreground);
  }

  .session-detail-value {
    font-size: 0.75rem;
    font-weight: 600;
  }

  .kill-user-btn {
    display: flex;
    align-items: center;
    padding: 0.5rem 0.875rem;
    background: color-mix(in oklch, var(--destructive) 8%, transparent);
    color: var(--destructive);
    border: 1px solid color-mix(in oklch, var(--destructive) 25%, transparent);
    border-radius: 0.5rem;
    font-size: 0.65rem;
    font-weight: 700;
    transition: all 0.2s;
  }

  .kill-user-btn:hover:not(:disabled) {
    background: var(--destructive);
    color: var(--primary-foreground);
    border-color: var(--destructive);
  }

  :global(.spinning) { animation: spin 1s linear infinite; }
  @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
</style>
