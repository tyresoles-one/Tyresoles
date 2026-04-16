<script lang="ts">
  import { onMount } from "svelte";
  import { slide } from "svelte/transition";
  import { graphqlQuery, graphqlMutation } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Toast } from "$lib/components/venUI/toast";
  import { Select } from "$lib/components/venUI/select";
  import NavEditTemplateDesigner from "$lib/components/navision-edits/NavEditTemplateDesigner.svelte";
  import {
    defaultEmptyTemplate,
    parseTemplate,
    serializeTemplate,
    validateTemplateForSave,
  } from "$lib/navision-edits/templateTypes";
  import type { NavEditFieldsTemplate } from "$lib/navision-edits/templateTypes";

  // ── Types ──────────────────────────────────────────────────
  interface RequestType {
    id: number;
    name: string;
    code: string;
    description?: string;
    icon?: string;
    navTable: string;
    navPrimaryKeyColumn: string;
    fieldsJson: string;
    isActive: boolean;
    sortOrder: number;
  }

  interface EditRequest {
    id: string;
    requestTypeId: number;
    requestTypeName: string;
    requestTypeCode: string;
    requestTypeIcon?: string;
    recordKey: string;
    requestBody: string;
    userId: string;
    userFullName?: string;
    status: number;
    statusLabel: string;
    remark?: string;
    adminRemark?: string;
    processedBy?: string;
    processedAt?: string;
    createdAt: string;
    updatedAt?: string;
    approvals: Approval[];
  }

  interface Approval {
    id: string;
    level: number;
    role: string;
    roleLabel?: string;
    approverUserIds?: string[];
    approvedBy?: string;
    status: number;
    statusLabel: string;
    comment?: string;
    actionDate?: string;
  }

  // ── GraphQL ────────────────────────────────────────────────
  const GET_REQUEST_TYPES = `
    query NavEditRequestTypes($activeOnly: Boolean) {
      navEditRequestTypes(activeOnly: $activeOnly) {
        id name code description icon navTable navPrimaryKeyColumn fieldsJson isActive sortOrder
      }
    }
  `;

  const SAVE_REQUEST_TYPE = `
    mutation NavEditSaveRequestType($input: NavEditRequestTypeInput!) {
      navEditSaveRequestType(input: $input) { success message }
    }
  `;

  const DELETE_REQUEST_TYPE = `
    mutation NavEditDeleteRequestType($id: Int!) {
      navEditDeleteRequestType(id: $id) { success message }
    }
  `;

  const GET_ALL_REQUESTS = `
    query NavEditAllRequests($statusFilter: Int) {
      navEditAllRequests(statusFilter: $statusFilter) {
        id requestTypeId requestTypeName requestTypeCode requestTypeIcon
        recordKey requestBody userId userFullName
        status statusLabel remark adminRemark processedBy processedAt
        createdAt updatedAt
        approvals { id level role roleLabel approverUserIds approvedBy status statusLabel comment actionDate }
      }
    }
  `;

  const APPROVE_REQUEST = `
    mutation NavEditApproveRequest($requestId: UUID!, $level: Int!, $comment: String) {
      navEditApproveRequest(requestId: $requestId, level: $level, comment: $comment) { success message }
    }
  `;

  const REJECT_REQUEST = `
    mutation NavEditRejectRequest($requestId: UUID!, $comment: String, $isApproval: Boolean, $level: Int) {
      navEditRejectRequest(requestId: $requestId, comment: $comment, isApproval: $isApproval, level: $level) { success message }
    }
  `;

  const PROCESS_REQUEST = `
    mutation NavEditProcessRequest($requestId: UUID!, $adminRemark: String) {
      navEditProcessRequest(requestId: $requestId, adminRemark: $adminRemark) { success message }
    }
  `;

  const GET_NAV_TABLES = `
    query { navEditNavTables }
  `;

  const GET_NAV_COLUMNS = `
    query NavEditNavTableColumns($tableName: String!) {
      navEditNavTableColumns(tableName: $tableName)
    }
  `;

  // ── State ──────────────────────────────────────────────────
  let activeTab = $state<'templates' | 'requests'>('requests');

  // Templates
  let requestTypes = $state<RequestType[]>([]);
  let typesLoading = $state(true);
  let editingType = $state<Partial<RequestType> | null>(null);
  let savingType = $state(false);
  let fieldsJsonText = $state("{}");
  let templateMode = $state<"designer" | "raw">("designer");
  let fieldsJsonModel = $state<NavEditFieldsTemplate>(defaultEmptyTemplate());

  let navTableOptions = $state<string[]>([]);
  let navColumnOptions = $state<string[]>([]);
  let navTablesLoading = $state(false);
  let navColumnsLoading = $state(false);

  function mergeTableOptions(current: string): string[] {
    const s = new Set(navTableOptions);
    const t = current?.trim();
    if (t) s.add(t);
    return [...s].sort((a, b) => a.localeCompare(b));
  }

  function mergePkOptions(current: string): string[] {
    const s = new Set(navColumnOptions);
    const t = current?.trim();
    if (t) s.add(t);
    return [...s].sort((a, b) => a.localeCompare(b));
  }

  $effect(() => {
    if (!editingType) {
      navTableOptions = [];
      navColumnOptions = [];
      navTablesLoading = false;
      navColumnsLoading = false;
      return;
    }
    let cancelled = false;
    navTablesLoading = true;
    (async () => {
      const res = await graphqlQuery<{ navEditNavTables: string[] }>(GET_NAV_TABLES, { skipCache: true });
      if (cancelled) return;
      if (res.success && res.data) navTableOptions = res.data.navEditNavTables;
      navTablesLoading = false;
    })();
    return () => {
      cancelled = true;
      navTablesLoading = false;
    };
  });

  $effect(() => {
    if (!editingType) return;
    const t = (editingType.navTable ?? "").trim();
    if (!t) {
      navColumnOptions = [];
      navColumnsLoading = false;
      return;
    }
    let cancelled = false;
    navColumnsLoading = true;
    (async () => {
      const res = await graphqlQuery<{ navEditNavTableColumns: string[] }>(GET_NAV_COLUMNS, {
        variables: { tableName: t },
        skipCache: true,
      });
      if (cancelled) return;
      if (res.success && res.data) navColumnOptions = res.data.navEditNavTableColumns;
      else navColumnOptions = [];
      navColumnsLoading = false;
    })();
    return () => {
      cancelled = true;
      navColumnsLoading = false;
    };
  });

  // Requests
  let requests = $state<EditRequest[]>([]);
  let requestsLoading = $state(true);
  let statusFilter = $state<number | null>(null);
  let expandedReqId = $state<string | null>(null);
  let actionComment = $state("");
  let actionLoading = $state<string | null>(null);
  let adminRemark = $state("");

  // ── Template CRUD ─────────────────────────────────────────
  async function fetchTypes() {
    typesLoading = true;
    const res = await graphqlQuery<{ navEditRequestTypes: RequestType[] }>(GET_REQUEST_TYPES, {
      variables: { activeOnly: false },
      skipCache: true,
    });
    if (res.success && res.data) requestTypes = res.data.navEditRequestTypes;
    typesLoading = false;
  }

  function startNewType() {
    fieldsJsonModel = defaultEmptyTemplate();
    fieldsJsonText = serializeTemplate(fieldsJsonModel);
    templateMode = "designer";
    editingType = {
      name: '',
      code: '',
      navTable: '',
      navPrimaryKeyColumn: '',
      fieldsJson: fieldsJsonText,
      isActive: true,
      sortOrder: 0,
    };
  }

  function startEditType(rt: RequestType) {
    const parsed = parseTemplate(rt.fieldsJson);
    if (parsed.ok) {
      fieldsJsonModel = parsed.data;
      fieldsJsonText = serializeTemplate(parsed.data);
      templateMode = "designer";
    } else {
      fieldsJsonText = rt.fieldsJson;
      fieldsJsonModel = defaultEmptyTemplate();
      templateMode = "raw";
      Toast.error(`Could not load template into designer: ${parsed.error}. Edit as raw JSON or fix syntax.`);
    }
    editingType = { ...rt };
  }

  function cancelEdit() {
    editingType = null;
    templateMode = "designer";
  }

  function switchTemplateToRaw() {
    fieldsJsonText = serializeTemplate(fieldsJsonModel);
    templateMode = "raw";
  }

  function switchTemplateToDesigner() {
    const p = parseTemplate(fieldsJsonText);
    if (!p.ok) {
      Toast.error(`Invalid JSON: ${p.error}`);
      return;
    }
    fieldsJsonModel = p.data;
    templateMode = "designer";
  }

  async function saveType() {
    if (!editingType) return;

    let fieldsJsonOut: string;
    if (templateMode === "designer") {
      const v = validateTemplateForSave(fieldsJsonModel);
      if (v) {
        Toast.error(v);
        return;
      }
      fieldsJsonOut = serializeTemplate(fieldsJsonModel);
    } else {
      try {
        JSON.parse(fieldsJsonText);
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        Toast.error(`Invalid JSON: ${msg}`);
        return;
      }
      const p = parseTemplate(fieldsJsonText);
      if (!p.ok) {
        Toast.error(`Invalid JSON: ${p.error}`);
        return;
      }
      const v = validateTemplateForSave(p.data);
      if (v) {
        Toast.error(v);
        return;
      }
      fieldsJsonOut = fieldsJsonText.trim();
    }

    savingType = true;
    const res = await graphqlMutation<{ navEditSaveRequestType: { success: boolean; message: string } }>(SAVE_REQUEST_TYPE, {
      variables: {
        input: {
          id: editingType.id || null,
          name: editingType.name || '',
          code: editingType.code || '',
          description: editingType.description || null,
          icon: editingType.icon || null,
          navTable: editingType.navTable || '',
          navPrimaryKeyColumn: editingType.navPrimaryKeyColumn || '',
          fieldsJson: fieldsJsonOut,
          isActive: editingType.isActive ?? true,
          sortOrder: editingType.sortOrder ?? 0,
        }
      }
    });
    if (res.success && res.data?.navEditSaveRequestType.success) {
      Toast.success("Request type saved!");
      editingType = null;
      fetchTypes();
    } else {
      Toast.error(res.data?.navEditSaveRequestType.message || res.error || "Save failed");
    }
    savingType = false;
  }

  async function deleteType(id: number) {
    if (!confirm("Deactivate this request type?")) return;
    const res = await graphqlMutation<{ navEditDeleteRequestType: { success: boolean; message: string } }>(DELETE_REQUEST_TYPE, {
      variables: { id }
    });
    if (res.success && res.data?.navEditDeleteRequestType.success) {
      Toast.success("Request type deactivated");
      fetchTypes();
    } else {
      Toast.error("Failed to deactivate");
    }
  }

  // ── Requests ──────────────────────────────────────────────
  async function fetchRequests() {
    requestsLoading = true;
    const res = await graphqlQuery<{ navEditAllRequests: EditRequest[] }>(GET_ALL_REQUESTS, {
      variables: { statusFilter },
      skipCache: true,
    });
    if (res.success && res.data) {
      requests = res.data.navEditAllRequests;
      const r = requests[0];
      if (r) {
        const ca = r.createdAt;
        const pa = r.processedAt;
        const dca = new Date(ca);
        const dpa = pa ? new Date(pa) : null;
        // #region agent log
        fetch("http://127.0.0.1:7618/ingest/5d806cd4-86b2-403a-9e2d-75847ea1b6fa", {
          method: "POST",
          headers: { "Content-Type": "application/json", "X-Debug-Session-Id": "ab180f" },
          body: JSON.stringify({
            sessionId: "ab180f",
            runId: "pre-fix",
            hypothesisId: "H2-H3",
            location: "navision-edits-admin/+page.svelte:fetchRequests",
            message: "nav edit queue first row datetimes",
            data: {
              createdAtRaw: ca,
              processedAtRaw: pa ?? null,
              createdEndsWithZ: typeof ca === "string" && ca.endsWith("Z"),
              parsedCreatedMs: dca.getTime(),
              parsedProcessedMs: dpa && !Number.isNaN(dpa.getTime()) ? dpa.getTime() : null,
              nowMs: Date.now(),
              deltaCreatedMs: Date.now() - dca.getTime(),
              tz: Intl.DateTimeFormat().resolvedOptions().timeZone,
              localeDateStringEnIn: (() => {
                try {
                  return new Date(ca).toLocaleDateString("en-IN", {
                    day: "2-digit",
                    month: "short",
                    year: "numeric",
                    hour: "2-digit",
                    minute: "2-digit",
                  });
                } catch {
                  return "err";
                }
              })(),
            },
            timestamp: Date.now(),
          }),
        }).catch(() => {});
        // #endregion
      }
    }
    requestsLoading = false;
  }

  async function approveRequest(reqId: string, level: number) {
    actionLoading = `approve-${reqId}-${level}`;
    const res = await graphqlMutation<{ navEditApproveRequest: { success: boolean; message: string } }>(APPROVE_REQUEST, {
      variables: { requestId: reqId, level, comment: actionComment || null }
    });
    if (res.success && res.data?.navEditApproveRequest.success) {
      Toast.success("Approved!");
      actionComment = "";
      fetchRequests();
    } else {
      Toast.error(res.data?.navEditApproveRequest.message || "Approval failed");
    }
    actionLoading = null;
  }

  async function rejectRequest(reqId: string, isApproval: boolean = false, level: number = 0) {
    actionLoading = `reject-${reqId}`;
    const res = await graphqlMutation<{ navEditRejectRequest: { success: boolean; message: string } }>(REJECT_REQUEST, {
      variables: { requestId: reqId, comment: actionComment || null, isApproval, level }
    });
    if (res.success && res.data?.navEditRejectRequest.success) {
      Toast.success("Rejected");
      actionComment = "";
      fetchRequests();
    } else {
      Toast.error(res.data?.navEditRejectRequest.message || "Rejection failed");
    }
    actionLoading = null;
  }

  async function processRequest(reqId: string) {
    actionLoading = `process-${reqId}`;
    const res = await graphqlMutation<{ navEditProcessRequest: { success: boolean; message: string } }>(PROCESS_REQUEST, {
      variables: { requestId: reqId, adminRemark: adminRemark || null }
    });
    if (res.success && res.data?.navEditProcessRequest.success) {
      Toast.success("Request marked as processed!");
      adminRemark = "";
      fetchRequests();
    } else {
      Toast.error(
        res.data?.navEditProcessRequest?.message ||
          res.error ||
          "Processing failed"
      );
    }
    actionLoading = null;
  }

  // ── Helpers ────────────────────────────────────────────────
  const statusLabels: Record<number, string> = { 0: 'Draft', 1: 'Pending', 2: 'Pending Approval', 3: 'Approved', 4: 'Processed', 5: 'Rejected' };
  const statusColors: Record<number, string> = {
    0: '#94a3b8', 1: '#f59e0b', 2: '#3b82f6', 3: '#10b981', 4: '#059669', 5: '#ef4444'
  };

  function getStatusStyle(s: number) {
    const c = statusColors[s] || '#94a3b8';
    return `background: ${c}15; color: ${c}; border: 1px solid ${c}30;`;
  }

  function formatDate(iso: string) {
    try { return new Date(iso).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }); }
    catch { return iso; }
  }

  function parseChanges(body: string): { column: string; label: string; oldValue: string; newValue: string }[] {
    try { return JSON.parse(body)?.changes ?? []; } catch { return []; }
  }

  function formatApprovalApprovers(a: Approval): string {
    if (a.approverUserIds?.length) return a.approverUserIds.join(', ');
    if (a.roleLabel || a.role) return a.roleLabel || a.role;
    return '—';
  }

  onMount(() => {
    fetchTypes();
    fetchRequests();
  });

  $effect(() => {
    // Re-fetch when filter changes
    const _ = statusFilter;
    fetchRequests();
  });
</script>

<svelte:head>
  <title>Navision Edits Admin</title>
</svelte:head>

<div class="nea-page">
  <PageHeading backHref="/" icon="shield-check">
    {#snippet title()}Navision Edit Requests — Admin{/snippet}
  </PageHeading>

  <!-- Tab Bar -->
  <div class="nea-tabs">
    <button class="nea-tab {activeTab === 'requests' ? 'nea-tab--active' : ''}" onclick={() => activeTab = 'requests'}>
      <Icon name="list-checks" class="nea-tab-icon" />
      Requests Queue
    </button>
    <button class="nea-tab {activeTab === 'templates' ? 'nea-tab--active' : ''}" onclick={() => activeTab = 'templates'}>
      <Icon name="settings-2" class="nea-tab-icon" />
      Manage Templates
    </button>
  </div>

  <div class="nea-content">
    {#if activeTab === 'requests'}
      <!-- ═══ REQUESTS QUEUE ═══════════════════════════════ -->
      <div class="nea-wrap">
        <!-- Filter bar -->
        <div class="nea-filter-bar">
          <span class="nea-filter-label">{requests.length} requests</span>
          <div class="nea-filter-chips">
            <button class="nea-chip {statusFilter === null ? 'nea-chip--active' : ''}" onclick={() => statusFilter = null}>All</button>
            {#each [1,2,3,4,5] as s}
              <button class="nea-chip {statusFilter === s ? 'nea-chip--active' : ''}" onclick={() => statusFilter = s} style={statusFilter === s ? getStatusStyle(s) : ''}>
                {statusLabels[s]}
              </button>
            {/each}
          </div>
          <button class="nea-refresh-btn" onclick={fetchRequests} disabled={requestsLoading}>
            <Icon name="refresh-cw" class="nea-refresh-icon {requestsLoading ? 'spinning' : ''}" />
          </button>
        </div>

        {#if requestsLoading}
          <div class="nea-skeleton-list">
            {#each { length: 5 } as _}<div class="nea-skeleton-row"></div>{/each}
          </div>
        {:else if requests.length === 0}
          <div class="nea-empty">
            <Icon name="inbox" class="nea-empty-icon" />
            <p>No requests found for this filter.</p>
          </div>
        {:else}
          <div class="nea-req-list">
            {#each requests as req (req.id)}
              <div class="nea-req-group">
                <button
                  class="nea-req-row {expandedReqId === req.id ? 'nea-req-row--expanded' : ''}"
                  onclick={() => expandedReqId = expandedReqId === req.id ? null : req.id}
                >
                  <div class="nea-req-icon-wrap">
                    <Icon name={req.requestTypeIcon || 'file-text'} class="w-5 h-5" />
                  </div>
                  <div class="nea-req-info">
                    <span class="nea-req-type">{req.requestTypeName}</span>
                    <span class="nea-req-meta">{req.userFullName || req.userId} → {req.recordKey}</span>
                  </div>
                  <span class="nea-status-badge" style={getStatusStyle(req.status)}>{req.statusLabel}</span>
                  <span class="nea-req-date">{formatDate(req.createdAt)}</span>
                </button>

                {#if expandedReqId === req.id}
                  <div class="nea-req-detail" transition:slide={{ duration: 200 }}>
                    <!-- Changes table -->
                    <div class="nea-changes-wrap">
                      <table class="nea-changes-table">
                        <thead><tr><th>Field</th><th>Old Value</th><th>New Value</th></tr></thead>
                        <tbody>
                          {#each parseChanges(req.requestBody) as c}
                            <tr>
                              <td class="nea-td-field">{c.label || c.column}</td>
                              <td class="nea-td-old">{c.oldValue || '—'}</td>
                              <td class="nea-td-new">{c.newValue || '—'}</td>
                            </tr>
                          {/each}
                        </tbody>
                      </table>
                    </div>

                    {#if req.remark}
                      <div class="nea-remark-box"><strong>User Remark:</strong> {req.remark}</div>
                    {/if}

                    <!-- Approvals -->
                    {#if req.approvals.length > 0}
                      <div class="nea-approvals-section">
                        <span class="nea-approvals-heading">Approval Chain</span>
                        {#each req.approvals as a}
                          <div class="nea-approval-row">
                            <span class="nea-approval-dot" style="background: {a.status === 1 ? '#10b981' : a.status === 2 ? '#ef4444' : '#94a3b8'}"></span>
                            <span class="nea-approval-info">
                              <strong>{formatApprovalApprovers(a)}</strong> (Level {a.level})
                              — {a.statusLabel}
                              {#if a.approvedBy} by {a.approvedBy}{/if}
                            </span>
                            {#if a.status === 0 && req.status === 2}
                              <div class="nea-approval-actions">
                                <button class="nea-action-approve" onclick={() => approveRequest(req.id, a.level)} disabled={actionLoading !== null}>
                                  {#if actionLoading === `approve-${req.id}-${a.level}`}
                                    <Icon name="loader-2" class="w-3 h-3 spinning" />
                                  {:else}
                                    Approve
                                  {/if}
                                </button>
                                <button class="nea-action-reject" onclick={() => rejectRequest(req.id, true, a.level)} disabled={actionLoading !== null}>
                                  Reject
                                </button>
                              </div>
                            {/if}
                          </div>
                        {/each}
                      </div>
                    {/if}

                    <!-- Admin actions -->
                    {#if req.status === 1 || req.status === 3}
                      <div class="nea-admin-actions">
                        <p class="nea-process-hint">
                          If this request type is configured with a NAV connector, <strong>Mark as Processed</strong> calls NAV first; the requester is notified only when NAV succeeds.
                        </p>
                        <input class="nea-admin-remark-input" type="text" placeholder="Admin remark (optional)…" bind:value={adminRemark} />
                        <div class="nea-admin-btns">
                          <button class="nea-action-process" onclick={() => processRequest(req.id)} disabled={actionLoading !== null}>
                            {#if actionLoading === `process-${req.id}`}
                              <Icon name="loader-2" class="w-4 h-4 spinning" />
                            {:else}
                              <Icon name="circle-check" class="w-4 h-4" />
                              Mark as Processed
                            {/if}
                          </button>
                          <button class="nea-action-reject-admin" onclick={() => rejectRequest(req.id)} disabled={actionLoading !== null}>
                            <Icon name="x-circle" class="w-4 h-4" />
                            Reject
                          </button>
                        </div>
                      </div>
                    {/if}

                    {#if req.adminRemark}
                      <div class="nea-admin-remark-display"><strong>Admin Remark:</strong> {req.adminRemark}</div>
                    {/if}
                    {#if req.processedBy}
                      <div class="nea-processed-info">Processed by {req.processedBy} on {req.processedAt ? formatDate(req.processedAt) : '—'}</div>
                    {/if}
                  </div>
                {/if}
              </div>
            {/each}
          </div>
        {/if}
      </div>

    {:else}
      <!-- ═══ TEMPLATES ═══════════════════════════════════ -->
      <div class="nea-wrap">
        {#if editingType}
          <!-- Template Editor -->
          <div class="nea-editor" transition:slide={{ duration: 200 }}>
            <div class="nea-editor-header">
              <h3 class="nea-editor-title">{editingType.id ? 'Edit' : 'New'} Request Type</h3>
              <button class="nea-editor-close" onclick={cancelEdit}>
                <Icon name="x" class="w-5 h-5" />
              </button>
            </div>

            <div class="nea-editor-grid">
              <div class="nea-form-group">
                <label class="nea-form-label">Name *</label>
                <input class="nea-form-input" bind:value={editingType.name} placeholder="e.g. Customer Master Edit" />
              </div>
              <div class="nea-form-group">
                <label class="nea-form-label">Code *</label>
                <input class="nea-form-input" bind:value={editingType.code} placeholder="e.g. CUSTOMER" />
              </div>
              <div class="nea-form-group">
                <label class="nea-form-label">Description</label>
                <input class="nea-form-input" bind:value={editingType.description} placeholder="Optional description" />
              </div>
              <div class="nea-form-group">
                <label class="nea-form-label">Icon (Lucide name)</label>
                <input class="nea-form-input" bind:value={editingType.icon} placeholder="e.g. users" />
              </div>
              <div class="nea-form-group">
                <label class="nea-form-label">Nav Table *</label>
                {#if navTablesLoading}
                  <p class="nea-inline-hint">Loading table list…</p>
                {/if}
                <div class="nea-select-wrap">
                  <Select
                    options={mergeTableOptions(editingType.navTable ?? "")}
                    bind:value={editingType.navTable}
                    placeholder="Select Nav table…"
                    clearable
                    searchPlaceholder="Search tables…"
                    emptyText="No tables found"
                    disabled={navTablesLoading}
                  />
                </div>
              </div>
              <div class="nea-form-group">
                <label class="nea-form-label">Primary Key Column *</label>
                {#if navColumnsLoading}
                  <p class="nea-inline-hint">Loading columns…</p>
                {/if}
                <div class="nea-select-wrap">
                  <Select
                    options={mergePkOptions(editingType.navPrimaryKeyColumn ?? "")}
                    bind:value={editingType.navPrimaryKeyColumn}
                    placeholder="Select primary key column…"
                    clearable
                    searchPlaceholder="Search columns…"
                    emptyText="No columns — choose Nav Table first"
                    disabled={navColumnsLoading || !(editingType.navTable ?? "").trim()}
                  />
                </div>
              </div>
              <div class="nea-form-group">
                <label class="nea-form-label">Sort Order</label>
                <input class="nea-form-input" type="number" bind:value={editingType.sortOrder} />
              </div>
              <div class="nea-form-group nea-form-checkbox">
                <label>
                  <input type="checkbox" bind:checked={editingType.isActive} />
                  Active
                </label>
              </div>
            </div>

            <div class="nea-form-group nea-form-full">
              <label class="nea-form-label">Fields template *</label>
              <div class="nea-template-mode-bar">
                <button
                  type="button"
                  class="nea-template-mode {templateMode === 'designer' ? 'nea-template-mode--active' : ''}"
                  onclick={() => {
                    if (templateMode === 'raw') switchTemplateToDesigner();
                  }}
                >
                  <Icon name="layout-template" class="w-3.5 h-3.5" />
                  Designer
                </button>
                <button
                  type="button"
                  class="nea-template-mode {templateMode === 'raw' ? 'nea-template-mode--active' : ''}"
                  onclick={() => {
                    if (templateMode === 'designer') switchTemplateToRaw();
                  }}
                >
                  <Icon name="code" class="w-3.5 h-3.5" />
                  Raw JSON
                </button>
                {#if templateMode === 'raw'}
                  <button type="button" class="nea-template-apply" onclick={switchTemplateToDesigner}>
                    Apply to designer
                  </button>
                {/if}
              </div>

              {#if templateMode === 'designer'}
                <div class="nea-designer-wrap">
                  <NavEditTemplateDesigner bind:model={fieldsJsonModel} columnOptions={navColumnOptions} />
                </div>
              {:else}
                <textarea class="nea-json-editor" bind:value={fieldsJsonText} rows="14" spellcheck="false"></textarea>
              {/if}
            
            </div>

            <div class="nea-editor-actions">
              <button class="nea-btn-cancel" onclick={cancelEdit}>Cancel</button>
              <button class="nea-btn-save" onclick={saveType} disabled={savingType}>
                {#if savingType}
                  <Icon name="loader-2" class="w-4 h-4 spinning" />
                {:else}
                  Save Template
                {/if}
              </button>
            </div>
          </div>
        {:else}
          <!-- Template List -->
          <div class="nea-template-toolbar">
            <span class="nea-template-count">{requestTypes.length} templates</span>
            <button class="nea-btn-new" onclick={startNewType}>
              <Icon name="plus" class="w-4 h-4" />
              New Template
            </button>
          </div>

          {#if typesLoading}
            <div class="nea-skeleton-list">
              {#each { length: 3 } as _}<div class="nea-skeleton-row"></div>{/each}
            </div>
          {:else if requestTypes.length === 0}
            <div class="nea-empty">
              <Icon name="layout-template" class="nea-empty-icon" />
              <p>No templates yet. Create one to get started.</p>
            </div>
          {:else}
            <div class="nea-template-list">
              {#each requestTypes as rt (rt.id)}
                <div class="nea-template-card">
                  <div class="nea-template-card-icon">
                    <Icon name={rt.icon || 'file-text'} class="w-5 h-5" />
                  </div>
                  <div class="nea-template-card-info">
                    <span class="nea-template-card-name">{rt.name}</span>
                    <span class="nea-template-card-meta">
                      {rt.code} · {rt.navTable}
                      {#if !rt.isActive}<span class="nea-inactive-badge">Inactive</span>{/if}
                    </span>
                  </div>
                  <div class="nea-template-card-actions">
                    <button class="nea-template-edit-btn" onclick={() => startEditType(rt)}>
                      <Icon name="pencil" class="w-4 h-4" />
                    </button>
                    {#if rt.isActive}
                      <button class="nea-template-delete-btn" onclick={() => deleteType(rt.id)}>
                        <Icon name="trash-2" class="w-4 h-4" />
                      </button>
                    {/if}
                  </div>
                </div>
              {/each}
            </div>
          {/if}
        {/if}
      </div>
    {/if}
  </div>
</div>

<style>
  .nea-page {
    min-height: 100svh;
    background: var(--background);
    color: var(--foreground);
    display: flex;
    flex-direction: column;
  }

  /* ── Tabs ──────────────────────────────────── */
  .nea-tabs {
    display: flex;
    border-bottom: 1px solid var(--border);
    background: var(--card);
    padding: 0 1.5rem;
    position: sticky;
    top: 0;
    z-index: 20;
  }

  .nea-tab {
    display: flex; align-items: center; gap: 0.5rem;
    padding: 0.875rem 1.25rem;
    font-size: 0.8rem; font-weight: 600;
    color: var(--muted-foreground);
    border: none; border-bottom: 2px solid transparent;
    background: none; cursor: pointer; transition: all 0.2s;
  }
  .nea-tab:hover { color: var(--foreground); }
  .nea-tab--active { color: var(--primary); border-bottom-color: var(--primary); }
  :global(.nea-tab-icon) { width: 1rem; height: 1rem; }

  .nea-content { flex: 1; overflow-y: auto; padding: 1.5rem; }
  .nea-wrap { max-width: 900px; margin: 0 auto; width: 100%; }

  /* ── Filter Bar ───────────────────────────── */
  .nea-filter-bar {
    display: flex; align-items: center; gap: 1rem;
    margin-bottom: 1rem; flex-wrap: wrap;
  }
  .nea-filter-label { font-size: 0.75rem; font-weight: 600; color: var(--muted-foreground); white-space: nowrap; }
  .nea-filter-chips { display: flex; gap: 0.375rem; flex-wrap: wrap; flex: 1; }

  .nea-chip {
    padding: 0.3rem 0.625rem; border-radius: 999px;
    font-size: 0.65rem; font-weight: 700;
    background: var(--muted); color: var(--muted-foreground);
    border: 1px solid var(--border); cursor: pointer; transition: all 0.2s;
  }
  .nea-chip:hover { border-color: var(--ring); }
  .nea-chip--active { background: var(--primary); color: var(--primary-foreground); border-color: var(--primary); }

  .nea-refresh-btn {
    width: 2rem; height: 2rem; border-radius: 50%;
    border: 1px solid var(--border); background: var(--card);
    display: flex; align-items: center; justify-content: center;
    color: var(--muted-foreground); cursor: pointer; transition: all 0.2s;
  }
  .nea-refresh-btn:hover { background: var(--muted); color: var(--foreground); }
  :global(.nea-refresh-icon) { width: 0.9rem; height: 0.9rem; }

  /* ── Request List ─────────────────────────── */
  .nea-req-list { display: flex; flex-direction: column; gap: 0.5rem; }
  .nea-req-group { display: flex; flex-direction: column; }

  .nea-req-row {
    display: flex; align-items: center; gap: 0.75rem;
    padding: 0.75rem 1rem;
    background: var(--card); border: 1px solid var(--border);
    border-radius: 0.75rem;
    cursor: pointer; transition: all 0.2s;
    width: 100%; text-align: left;
  }
  .nea-req-row:hover { border-color: var(--ring); box-shadow: 0 4px 12px rgba(0,0,0,0.04); }
  .nea-req-row--expanded { border-color: var(--primary); border-bottom-left-radius: 0; border-bottom-right-radius: 0; }

  .nea-req-icon-wrap {
    width: 2rem; height: 2rem; border-radius: 0.5rem;
    background: var(--muted); display: flex; align-items: center; justify-content: center;
    color: var(--primary); flex-shrink: 0;
  }

  .nea-req-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
  .nea-req-type { font-size: 0.8rem; font-weight: 600; }
  .nea-req-meta { font-size: 0.65rem; color: var(--muted-foreground); }

  .nea-status-badge {
    padding: 0.2rem 0.5rem; border-radius: 999px;
    font-size: 0.6rem; font-weight: 700; white-space: nowrap; flex-shrink: 0;
  }

  .nea-req-date { font-size: 0.6rem; color: var(--muted-foreground); white-space: nowrap; flex-shrink: 0; }

  /* ── Request Detail ───────────────────────── */
  .nea-req-detail {
    background: var(--card); border: 1px solid var(--primary); border-top: none;
    border-bottom-left-radius: 0.75rem; border-bottom-right-radius: 0.75rem;
    padding: 1rem;
  }

  .nea-changes-wrap { overflow-x: auto; }
  .nea-changes-table { width: 100%; border-collapse: collapse; font-size: 0.75rem; }
  .nea-changes-table th { text-align: left; padding: 0.4rem; font-size: 0.6rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted-foreground); font-weight: 700; border-bottom: 1px solid var(--border); }
  .nea-changes-table td { padding: 0.4rem; border-bottom: 1px solid var(--border); }
  .nea-td-field { font-weight: 600; }
  .nea-td-old { color: var(--muted-foreground); text-decoration: line-through; }
  .nea-td-new { color: var(--primary); font-weight: 600; }

  .nea-remark-box { margin-top: 0.75rem; font-size: 0.75rem; padding: 0.5rem 0.75rem; background: var(--muted); border-radius: 0.375rem; }

  /* ── Approvals ────────────────────────────── */
  .nea-approvals-section { margin-top: 0.75rem; display: flex; flex-direction: column; gap: 0.4rem; }
  .nea-approvals-heading { font-size: 0.6rem; font-weight: 700; text-transform: uppercase; color: var(--muted-foreground); letter-spacing: 0.04em; }
  .nea-approval-row { display: flex; align-items: center; gap: 0.5rem; font-size: 0.75rem; flex-wrap: wrap; }
  .nea-approval-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
  .nea-approval-info { flex: 1; }
  .nea-approval-actions { display: flex; gap: 0.375rem; }

  .nea-action-approve {
    padding: 0.25rem 0.625rem; font-size: 0.65rem; font-weight: 700;
    background: #10b98120; color: #10b981; border: 1px solid #10b98130;
    border-radius: 0.375rem; cursor: pointer; transition: all 0.2s;
  }
  .nea-action-approve:hover:not(:disabled) { background: #10b981; color: white; }

  .nea-action-reject {
    padding: 0.25rem 0.625rem; font-size: 0.65rem; font-weight: 700;
    background: #ef444420; color: #ef4444; border: 1px solid #ef444430;
    border-radius: 0.375rem; cursor: pointer; transition: all 0.2s;
  }
  .nea-action-reject:hover:not(:disabled) { background: #ef4444; color: white; }

  /* ── Admin Actions ────────────────────────── */
  .nea-admin-actions {
    margin-top: 1rem; padding-top: 0.75rem; border-top: 1px solid var(--border);
    display: flex; flex-direction: column; gap: 0.5rem;
  }
  .nea-process-hint {
    margin: 0;
    font-size: 0.7rem;
    line-height: 1.35;
    color: var(--muted-foreground);
  }
  .nea-admin-remark-input {
    width: 100%; padding: 0.5rem 0.75rem; font-size: 0.8rem;
    border: 1px solid var(--border); border-radius: 0.5rem;
    background: var(--background); outline: none;
  }
  .nea-admin-remark-input:focus { border-color: var(--primary); }
  .nea-admin-btns { display: flex; gap: 0.5rem; }

  .nea-action-process {
    display: flex; align-items: center; gap: 0.375rem;
    padding: 0.5rem 1rem; font-size: 0.75rem; font-weight: 700;
    background: var(--primary); color: var(--primary-foreground);
    border: none; border-radius: 0.5rem; cursor: pointer; transition: all 0.2s;
  }
  .nea-action-process:hover:not(:disabled) { opacity: 0.9; }
  .nea-action-process:disabled { opacity: 0.5; cursor: not-allowed; }

  .nea-action-reject-admin {
    display: flex; align-items: center; gap: 0.375rem;
    padding: 0.5rem 1rem; font-size: 0.75rem; font-weight: 700;
    background: #ef444415; color: #ef4444; border: 1px solid #ef444430;
    border-radius: 0.5rem; cursor: pointer; transition: all 0.2s;
  }
  .nea-action-reject-admin:hover:not(:disabled) { background: #ef4444; color: white; }

  .nea-admin-remark-display { margin-top: 0.5rem; font-size: 0.75rem; padding: 0.4rem 0.75rem; background: color-mix(in srgb, #f59e0b 8%, var(--background)); border-radius: 0.375rem; }
  .nea-processed-info { margin-top: 0.4rem; font-size: 0.65rem; color: var(--muted-foreground); }

  /* ── Template Editor ──────────────────────── */
  .nea-editor {
    background: var(--card); border: 1px solid var(--border);
    border-radius: 1rem; padding: 1.5rem;
  }

  .nea-editor-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
  .nea-editor-title { font-size: 1rem; font-weight: 700; margin: 0; }
  .nea-editor-close { background: none; border: none; color: var(--muted-foreground); cursor: pointer; padding: 0.25rem; border-radius: 0.375rem; }
  .nea-editor-close:hover { color: var(--foreground); background: var(--muted); }

  .nea-editor-grid {
    display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;
    margin-bottom: 1rem;
  }

  .nea-form-group { display: flex; flex-direction: column; gap: 0.3rem; }
  .nea-form-full { grid-column: 1 / -1; }
  .nea-form-label { font-size: 0.65rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted-foreground); }
  .nea-form-input {
    padding: 0.5rem 0.75rem; font-size: 0.8rem;
    border: 1px solid var(--border); border-radius: 0.5rem;
    background: var(--background); outline: none; font-family: inherit;
  }
  .nea-form-input:focus { border-color: var(--primary); }
  .nea-select-wrap {
    width: 100%;
    min-width: 0;
  }
  .nea-inline-hint {
    margin: 0;
    font-size: 0.6rem;
    color: var(--muted-foreground);
  }
  .nea-form-checkbox { justify-content: flex-end; flex-direction: row; align-items: center; gap: 0.5rem; }
  .nea-form-checkbox label { font-size: 0.8rem; font-weight: 600; display: flex; align-items: center; gap: 0.375rem; }

  .nea-json-editor {
    width: 100%; padding: 0.75rem; font-size: 0.75rem;
    font-family: 'Consolas', 'Monaco', monospace;
    border: 1px solid var(--border); border-radius: 0.5rem;
    background: var(--background); outline: none;
    resize: vertical; line-height: 1.5;
  }
  .nea-json-editor:focus { border-color: var(--primary); }

  .nea-template-mode-bar {
    display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem;
    margin-bottom: 0.75rem;
  }
  .nea-template-mode {
    display: inline-flex; align-items: center; gap: 0.35rem;
    padding: 0.4rem 0.75rem; font-size: 0.7rem; font-weight: 600;
    border: 1px solid var(--border); border-radius: 0.5rem;
    background: var(--muted); color: var(--muted-foreground);
    cursor: pointer;
  }
  .nea-template-mode:hover { border-color: var(--ring); color: var(--foreground); }
  .nea-template-mode--active {
    background: var(--primary); color: var(--primary-foreground); border-color: var(--primary);
  }
  .nea-template-apply {
    font-size: 0.65rem; font-weight: 600;
    padding: 0.35rem 0.6rem;
    border: 1px dashed var(--border); border-radius: 0.375rem;
    background: transparent; color: var(--primary); cursor: pointer;
  }
  .nea-template-apply:hover { border-color: var(--primary); }
  .nea-designer-wrap {
    border: 1px solid var(--border); border-radius: 0.75rem;
    padding: 1rem;
    background: var(--card);
  }

  .nea-json-hint { font-size: 0.65rem; color: var(--muted-foreground); margin-top: 0.25rem; }
  .nea-json-hint code { background: var(--muted); padding: 0.1rem 0.3rem; border-radius: 0.25rem; font-size: 0.6rem; }

  .nea-editor-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1rem; }

  .nea-btn-cancel {
    padding: 0.5rem 1.25rem; font-size: 0.8rem; font-weight: 600;
    background: var(--muted); color: var(--foreground);
    border: 1px solid var(--border); border-radius: 0.5rem; cursor: pointer;
  }
  .nea-btn-cancel:hover { background: var(--background); }

  .nea-btn-save {
    display: flex; align-items: center; gap: 0.375rem;
    padding: 0.5rem 1.25rem; font-size: 0.8rem; font-weight: 700;
    background: var(--primary); color: var(--primary-foreground);
    border: none; border-radius: 0.5rem; cursor: pointer; transition: all 0.2s;
  }
  .nea-btn-save:hover:not(:disabled) { opacity: 0.9; }
  .nea-btn-save:disabled { opacity: 0.5; cursor: not-allowed; }

  /* ── Template List ────────────────────────── */
  .nea-template-toolbar {
    display: flex; align-items: center; justify-content: space-between;
    margin-bottom: 1rem;
  }
  .nea-template-count { font-size: 0.75rem; font-weight: 600; color: var(--muted-foreground); }

  .nea-btn-new {
    display: flex; align-items: center; gap: 0.375rem;
    padding: 0.5rem 1rem; font-size: 0.8rem; font-weight: 700;
    background: var(--primary); color: var(--primary-foreground);
    border: none; border-radius: 0.5rem; cursor: pointer; transition: all 0.2s;
  }
  .nea-btn-new:hover { opacity: 0.9; }

  .nea-template-list { display: flex; flex-direction: column; gap: 0.5rem; }

  .nea-template-card {
    display: flex; align-items: center; gap: 0.75rem;
    padding: 0.875rem 1rem;
    background: var(--card); border: 1px solid var(--border);
    border-radius: 0.75rem; transition: all 0.2s;
  }
  .nea-template-card:hover { border-color: var(--ring); }

  .nea-template-card-icon {
    width: 2.25rem; height: 2.25rem; border-radius: 0.5rem;
    background: var(--muted); display: flex; align-items: center;
    justify-content: center; color: var(--primary); flex-shrink: 0;
  }

  .nea-template-card-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
  .nea-template-card-name { font-size: 0.85rem; font-weight: 600; }
  .nea-template-card-meta { font-size: 0.65rem; color: var(--muted-foreground); }

  .nea-inactive-badge {
    display: inline-block; padding: 0.1rem 0.4rem; margin-left: 0.5rem;
    background: #ef444420; color: #ef4444; border-radius: 999px;
    font-size: 0.55rem; font-weight: 700;
  }

  .nea-template-card-actions { display: flex; gap: 0.375rem; }

  .nea-template-edit-btn, .nea-template-delete-btn {
    width: 2rem; height: 2rem; border-radius: 0.5rem;
    display: flex; align-items: center; justify-content: center;
    border: 1px solid var(--border); background: var(--background);
    color: var(--muted-foreground); cursor: pointer; transition: all 0.2s;
  }
  .nea-template-edit-btn:hover { color: var(--primary); border-color: var(--primary); }
  .nea-template-delete-btn:hover { color: #ef4444; border-color: #ef4444; }

  /* ── Empty & Skeleton ─────────────────────── */
  .nea-empty {
    display: flex; flex-direction: column; align-items: center;
    justify-content: center; padding: 3rem 1rem; color: var(--muted-foreground); text-align: center;
  }
  :global(.nea-empty-icon) { width: 3rem; height: 3rem; margin-bottom: 0.75rem; opacity: 0.3; }
  .nea-empty p { font-size: 0.85rem; }

  .nea-skeleton-list { display: flex; flex-direction: column; gap: 0.5rem; }
  .nea-skeleton-row { height: 3.5rem; border-radius: 0.75rem; background: var(--muted); animation: pulse 1.5s ease-in-out infinite; }

  :global(.spinning) { animation: spin 1s linear infinite; }
  @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
  @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.5; } }

  @media (max-width: 640px) {
    .nea-content { padding: 1rem; }
    .nea-editor-grid { grid-template-columns: 1fr; }
    .nea-filter-bar { flex-direction: column; align-items: flex-start; }
    .nea-req-row { flex-wrap: wrap; }
    .nea-admin-btns { flex-direction: column; }
  }
</style>
