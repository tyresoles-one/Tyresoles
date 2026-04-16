<script lang="ts">
  import { onMount } from "svelte";
  import { get } from "svelte/store";
  import { slide } from "svelte/transition";
  import { authStore } from "$lib/stores/auth";
  import { graphqlQuery, graphqlMutation } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Toast } from "$lib/components/venUI/toast";
  import NavEditMasterField from "$lib/components/navision-edits/NavEditMasterField.svelte";
  import { parseNavEditFieldMasterType } from "$lib/navision-edits/nav-field-types";
  import {
    parseTemplate,
    defaultEmptyTemplate,
    type NavEditFieldsTemplate,
    type NavEditDisplayColumnSpec,
  } from "$lib/navision-edits/templateTypes";
  import { normalizeNavDateToIsoDay } from "$lib/navision-edits/nav-date";
  import {
    formatNavDisplayColumnValue,
    formatForDisplayColumnKey,
  } from "$lib/navision-edits/display-format";
  import { EmptyState } from "$lib/components/venUI/emptyState";
  import { Select as VenSelect } from "$lib/components/venUI/select";
  import { DatePicker } from "$lib/components/venUI/date-picker";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Textarea } from "$lib/components/ui/textarea";
  import { Label } from "$lib/components/ui/label";
  import { Badge } from "$lib/components/ui/badge";
  import { Card, CardContent, CardHeader, CardTitle } from "$lib/components/ui/card";
  import { Tabs, TabsContent, TabsList, TabsTrigger } from "$lib/components/ui/tabs";
  import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "$lib/components/ui/table";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { cn } from "$lib/utils";
  import Loader2 from "@lucide/svelte/icons/loader-2";

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

  interface KVItem {
    key: string;
    value?: string;
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

  const LOOKUP_RECORDS = `
    query NavEditLookupRecords($requestTypeId: Int!, $search: String, $take: Int) {
      navEditLookupRecords(requestTypeId: $requestTypeId, search: $search, take: $take) {
        key value
      }
    }
  `;

  const GET_RECORD = `
    query NavEditGetRecord($requestTypeId: Int!, $recordKey: String!) {
      navEditGetRecord(requestTypeId: $requestTypeId, recordKey: $recordKey) {
        key value
      }
    }
  `;

  const SUBMIT_REQUEST = `
    mutation NavEditSubmitRequest($input: NavEditRequestInput!) {
      navEditSubmitRequest(input: $input) {
        success message
      }
    }
  `;

  /** Max rows returned from Nav for find-record lookup (matches UI “up to 10”). */
  const LOOKUP_TAKE = 10;

  const GET_MY_REQUESTS = `
    query NavEditMyRequests {
      navEditMyRequests {
        id requestTypeId requestTypeName requestTypeCode requestTypeIcon
        recordKey requestBody userId userFullName
        status statusLabel remark adminRemark processedBy processedAt
        createdAt updatedAt
        approvals { id level role roleLabel approverUserIds approvedBy status statusLabel comment actionDate }
      }
    }
  `;

  const RESEND_NOTIFICATIONS = `
    mutation NavEditResendNotifications($requestId: UUID!) {
      navEditResendNotifications(requestId: $requestId) {
        success message
      }
    }
  `;

  // ── State ──────────────────────────────────────────────────
  let activeTab = $state<'new' | 'my'>('new');
  let requestTypes = $state<RequestType[]>([]);
  let selectedTypeId = $state<number | null>(null);
  let templateConfig = $state<NavEditFieldsTemplate | null>(null);
  let loading = $state(true);
  let submitting = $state(false);

  // Search & record (edit existing)
  let searchTerm = $state("");
  let searchResults = $state<KVItem[][]>([]);
  let searchLoading = $state(false);
  let selectedRecord = $state<Record<string, string | null> | null>(null);
  let recordLoading = $state(false);

  /** When template allows: edit existing row vs request a new row (optional copy-from). */
  let workflowMode = $state<"edit" | "create">("edit");
  let sourceRecordKey = $state<string | null>(null);
  let newRecordKey = $state("");
  let copySearchTerm = $state("");
  let copySearchResults = $state<KVItem[][]>([]);
  let copySearchLoading = $state(false);

  // Edit form
  let editValues = $state<Record<string, string>>({});
  let remark = $state("");

  // My requests
  let myRequests = $state<EditRequest[]>([]);
  let myRequestsLoading = $state(false);
  let expandedRequestId = $state<string | null>(null);
  let resendingRequestId = $state<string | null>(null);

  // ── Data Fetching ──────────────────────────────────────────
  async function fetchRequestTypes() {
    loading = true;
    const res = await graphqlQuery<{ navEditRequestTypes: RequestType[] }>(GET_REQUEST_TYPES, {
      variables: { activeOnly: true },
      skipCache: true,
    });
    if (res.success && res.data) {
      requestTypes = res.data.navEditRequestTypes;
    }
    loading = false;
  }

  function selectType(id: number) {
    selectedTypeId = id;
    const rt = requestTypes.find(t => t.id === id);
    if (rt) {
      const parsed = parseTemplate(rt.fieldsJson);
      templateConfig = parsed.ok ? parsed.data : defaultEmptyTemplate();
    }
    // Reset
    workflowMode = "edit";
    sourceRecordKey = null;
    newRecordKey = "";
    copySearchTerm = "";
    copySearchResults = [];
    searchTerm = "";
    searchResults = [];
    selectedRecord = null;
    editValues = {};
    remark = "";

    // If "Posting Dates", auto-search by current user ID (edit flow only)
    if (rt && rt.name === "Posting Dates") {
      const user = get(authStore).user;
      if (user?.userId) {
        searchTerm = user.userId;
        void runLiveLookup();
      }
    }
  }

  function setWorkflowMode(mode: "edit" | "create") {
    workflowMode = mode;
    if (mode === "edit") {
      sourceRecordKey = null;
      newRecordKey = "";
      copySearchTerm = "";
      copySearchResults = [];
      selectedRecord = null;
      editValues = {};
    } else {
      searchTerm = "";
      searchResults = [];
      selectedRecord = null;
      sourceRecordKey = null;
      newRecordKey = "";
      copySearchTerm = "";
      copySearchResults = [];
      editValues = {};
    }
  }

  function primaryKeyColumn(): string {
    const rt = selectedTypeId ? requestTypes.find((t) => t.id === selectedTypeId) : undefined;
    return rt?.navPrimaryKeyColumn?.trim() ?? "";
  }

  const LIVE_SEARCH_DEBOUNCE_MS = 200;
  let searchDebounceTimer: ReturnType<typeof setTimeout> | undefined;

  /** Debounced live lookup as the user types; clears immediately when the box is empty. */
  function scheduleLiveSearch() {
    if (workflowMode === "create") return;
    clearTimeout(searchDebounceTimer);
    const q = searchTerm.trim();
    if (!q) {
      searchResults = [];
      searchLoading = false;
      return;
    }
    searchDebounceTimer = setTimeout(() => {
      searchDebounceTimer = undefined;
      void runLiveLookup();
    }, LIVE_SEARCH_DEBOUNCE_MS);
  }

  /** Same debounced pipeline when the field is focused with text (e.g. returning to the field). */
  function onSearchFocus() {
    if (workflowMode === "create") return;
    if (searchTerm.trim() && selectedTypeId) scheduleLiveSearch();
  }

  let copyDebounceTimer: ReturnType<typeof setTimeout> | undefined;

  function scheduleCopySearch() {
    clearTimeout(copyDebounceTimer);
    const q = copySearchTerm.trim();
    if (!q) {
      copySearchResults = [];
      copySearchLoading = false;
      return;
    }
    copyDebounceTimer = setTimeout(() => {
      copyDebounceTimer = undefined;
      void runLiveCopyLookup();
    }, LIVE_SEARCH_DEBOUNCE_MS);
  }

  function onCopySearchFocus() {
    if (copySearchTerm.trim() && selectedTypeId) scheduleCopySearch();
  }

  /** Columns Nav uses for substring search: template `searchColumns`, or primary key only if unset. */
  function getSearchableColumnsDisplay(): string[] {
    const rt = selectedTypeId ? requestTypes.find((t) => t.id === selectedTypeId) : undefined;
    const pk = rt?.navPrimaryKeyColumn?.trim() ?? "";
    const configured = templateConfig?.searchColumns?.map((c) => c.trim()).filter(Boolean) ?? [];
    if (configured.length > 0) return configured;
    return pk ? [pk] : [];
  }

  async function runLiveLookup() {
    if (workflowMode === "create") {
      searchLoading = false;
      return;
    }
    const term = searchTerm.trim();
    const typeId = selectedTypeId;
    if (!typeId || !term) {
      searchResults = [];
      searchLoading = false;
      return;
    }
    searchLoading = true;
    const res = await graphqlQuery<{ navEditLookupRecords: KVItem[][] }>(LOOKUP_RECORDS, {
      variables: { requestTypeId: typeId, search: term, take: LOOKUP_TAKE },
      skipCache: true,
    });
    // Drop stale responses if the user kept typing or switched request type
    if (selectedTypeId !== typeId || searchTerm.trim() !== term) {
      searchLoading = false;
      return;
    }
    if (res.success && res.data) {
      searchResults = res.data.navEditLookupRecords;
    }
    searchLoading = false;
  }

  async function runLiveCopyLookup() {
    const term = copySearchTerm.trim();
    const typeId = selectedTypeId;
    if (!typeId || !term || workflowMode !== "create") {
      copySearchResults = [];
      copySearchLoading = false;
      return;
    }
    copySearchLoading = true;
    const res = await graphqlQuery<{ navEditLookupRecords: KVItem[][] }>(LOOKUP_RECORDS, {
      variables: { requestTypeId: typeId, search: term, take: LOOKUP_TAKE },
      skipCache: true,
    });
    if (selectedTypeId !== typeId || copySearchTerm.trim() !== term) {
      copySearchLoading = false;
      return;
    }
    if (res.success && res.data) {
      copySearchResults = res.data.navEditLookupRecords;
    }
    copySearchLoading = false;
  }

  async function selectRecord(row: KVItem[]) {
    if (!selectedTypeId || workflowMode !== "edit") return;
    const rt = requestTypes.find(t => t.id === selectedTypeId);
    if (!rt) return;

    const pk = row.find(kv => kv.key.toLowerCase().replace(/[_ ]/g, '') === rt.navPrimaryKeyColumn.toLowerCase().replace(/[_ ]/g, ''));
    const key = pk?.value ?? row[0]?.value;
    if (!key) return;

    recordLoading = true;
    searchResults = [];
    searchTerm = key;

    const res = await graphqlQuery<{ navEditGetRecord: KVItem[] }>(GET_RECORD, {
      variables: { requestTypeId: selectedTypeId, recordKey: key },
      skipCache: true,
    });
    if (res.success && res.data?.navEditGetRecord) {
      const record: Record<string, string | null> = {};
      for (const kv of res.data.navEditGetRecord) {
        record[kv.key] = kv.value ?? null;
      }
      selectedRecord = record;
      // Pre-fill edit values
      editValues = {};
      if (templateConfig?.fields) {
        for (const f of templateConfig.fields) {
          let v = record[f.column] ?? "";
          if (f.type === "date" && typeof v === "string" && v) {
            v = normalizeNavDateToIsoDay(v);
          }
          editValues[f.column] = v;
        }
      }
      applyAuthPrefillsAfterRecordLoad();
    }
    recordLoading = false;
  }

  /** Load field values from an existing row to start a new-record request (optional). */
  async function selectRecordFromCopy(row: KVItem[]) {
    if (!selectedTypeId || workflowMode !== "create") return;
    const rt = requestTypes.find((t) => t.id === selectedTypeId);
    if (!rt) return;

    const pk = row.find(
      (kv) =>
        kv.key.toLowerCase().replace(/[_ ]/g, "") === rt.navPrimaryKeyColumn.toLowerCase().replace(/[_ ]/g, "")
    );
    const key = pk?.value ?? row[0]?.value;
    if (!key) return;

    recordLoading = true;
    copySearchResults = [];
    copySearchTerm = key;
    sourceRecordKey = key;

    const res = await graphqlQuery<{ navEditGetRecord: KVItem[] }>(GET_RECORD, {
      variables: { requestTypeId: selectedTypeId, recordKey: key },
      skipCache: true,
    });
    if (res.success && res.data?.navEditGetRecord) {
      const record: Record<string, string | null> = {};
      for (const kv of res.data.navEditGetRecord) {
        record[kv.key] = kv.value ?? null;
      }
      selectedRecord = record;
      editValues = {};
      if (templateConfig?.fields) {
        for (const f of templateConfig.fields) {
          let v = record[f.column] ?? "";
          if (f.type === "date" && typeof v === "string" && v) {
            v = normalizeNavDateToIsoDay(v);
          }
          editValues[f.column] = v;
        }
      }
      const pkCol = rt.navPrimaryKeyColumn.trim();
      if (pkCol) {
        editValues[pkCol] = "";
      }
      newRecordKey = "";
      applyAuthPrefillsAfterRecordLoad();
    }
    recordLoading = false;
  }

  /**
   * After choosing a row from search: prefill empty user master fields with current Nav user id;
   * for date columns whose name suggests posting date, default from session work date when empty.
   */
  function applyAuthPrefillsAfterRecordLoad() {
    if (!templateConfig?.fields?.length) return;
    const user = get(authStore).user;
    if (!user) return;

    for (const f of templateConfig.fields) {
      const master = parseNavEditFieldMasterType(f.type);
      const cur = (editValues[f.column] ?? "").trim();

      if (master === "users" && !cur) {
        editValues[f.column] = user.userId;
      }

      if (f.type === "date" && !cur && /posting/i.test(f.column) && user.workDate) {
        editValues[f.column] = normalizeNavDateToIsoDay(String(user.workDate));
      }
    }
    editValues = { ...editValues };
  }

  async function submitRequest() {
    if (!selectedTypeId || !templateConfig) return;

    const rt = requestTypes.find((t) => t.id === selectedTypeId);
    if (!rt) return;

    const pk = rt.navPrimaryKeyColumn.trim();
    const isCreate = workflowMode === "create" && templateConfig.allowNewRecordCreate;

    if (isCreate) {
      const nk = (newRecordKey.trim() || (pk ? (editValues[pk] ?? "").trim() : "")).trim();
      if (!nk) {
        Toast.error(`Enter the new ${pk || "primary key"} for the new record.`);
        return;
      }
      if (pk) editValues[pk] = nk;
      newRecordKey = nk;
    } else {
      if (!selectedRecord || !searchTerm.trim()) return;
    }

    const baseRecord = isCreate ? (selectedRecord ?? {}) : selectedRecord!;

    const changes: { column: string; label: string; oldValue: string; newValue: string }[] = [];
    if (templateConfig.fields) {
      for (const f of templateConfig.fields) {
        const oldVal = baseRecord[f.column] ?? "";
        const newVal = editValues[f.column] ?? "";
        const same =
          f.type === "date"
            ? normalizeNavDateToIsoDay(String(oldVal)) === normalizeNavDateToIsoDay(String(newVal))
            : String(oldVal) === String(newVal);
        if (!same) {
          changes.push({ column: f.column, label: f.label, oldValue: String(oldVal), newValue: String(newVal) });
        }
      }
    }

    if (changes.length === 0) {
      Toast.error(
        isCreate
          ? "No field values entered. Enter the new key and at least one field, or copy from an existing row."
          : "No changes detected. Please modify at least one field."
      );
      return;
    }

    const recordKeyForSubmit = isCreate ? (pk ? (editValues[pk] ?? newRecordKey).trim() : newRecordKey.trim()) : searchTerm.trim();

    const requestBody = JSON.stringify({
      mode: isCreate ? "create" : "edit",
      ...(isCreate && sourceRecordKey ? { sourceRecordKey } : {}),
      changes,
    });

    submitting = true;
    const res = await graphqlMutation<{ navEditSubmitRequest: { success: boolean; message: string } }>(SUBMIT_REQUEST, {
      variables: {
        input: {
          requestTypeId: selectedTypeId,
          recordKey: recordKeyForSubmit,
          requestBody,
          remark: remark || null,
        }
      }
    });

    if (res.success && res.data?.navEditSubmitRequest.success) {
      Toast.success("Request submitted successfully!");
      // Reset form
      selectedRecord = null;
      editValues = {};
      remark = "";
      searchTerm = "";
      copySearchTerm = "";
      copySearchResults = [];
      sourceRecordKey = null;
      newRecordKey = "";
      setWorkflowMode("edit");
      // Switch to my requests
      activeTab = 'my';
      fetchMyRequests();
    } else {
      Toast.error(res.data?.navEditSubmitRequest.message || res.error || "Failed to submit request");
    }
    submitting = false;
  }

  async function fetchMyRequests() {
    myRequestsLoading = true;
    const res = await graphqlQuery<{ navEditMyRequests: EditRequest[] }>(GET_MY_REQUESTS, { skipCache: true });
    if (res.success && res.data) {
      myRequests = res.data.navEditMyRequests;
    }
    myRequestsLoading = false;
  }

  /** Pending (1) or PendingApproval (2): requester may resend IT / approver notifications. */
  function canResendNotifications(req: EditRequest): boolean {
    return req.status === 1 || req.status === 2;
  }

  async function resendNotifications(req: EditRequest) {
    resendingRequestId = req.id;
    const res = await graphqlMutation<{
      navEditResendNotifications: { success: boolean; message: string };
    }>(RESEND_NOTIFICATIONS, { variables: { requestId: req.id }, silent: true });
    resendingRequestId = null;
    if (res.success && res.data?.navEditResendNotifications.success) {
      Toast.success(res.data.navEditResendNotifications.message || "Notifications sent.");
    } else {
      Toast.error(
        res.data?.navEditResendNotifications.message || res.error || "Could not resend notifications."
      );
    }
  }

  // ── Helpers ────────────────────────────────────────────────
  /** Order lookup cells by template displayColumns; include PK if returned but not listed; append any extras. */
  function orderLookupRowKvs(
    row: KVItem[],
    displayColumns: NavEditDisplayColumnSpec[] | undefined,
    pkColumn: string | undefined
  ): KVItem[] {
    const specs = displayColumns?.filter((s) => s.column?.trim()) ?? [];
    if (specs.length === 0) return row;
    const find = (col: string) =>
      row.find((k) => k.key.trim().toLowerCase() === col.trim().toLowerCase());
    const out: KVItem[] = [];
    const used = new Set<string>();
    for (const s of specs) {
      const kv = find(s.column);
      if (kv) {
        out.push(kv);
        used.add(kv.key.trim().toLowerCase());
      }
    }
    const pk = pkColumn?.trim() ? find(pkColumn) : undefined;
    if (pk && !used.has(pk.key.trim().toLowerCase())) {
      out.unshift(pk);
      used.add(pk.key.trim().toLowerCase());
    }
    for (const kv of row) {
      if (!used.has(kv.key.trim().toLowerCase())) out.push(kv);
    }
    return out;
  }

  /** Tailwind classes for Nav edit request status badges */
  function statusBadgeClass(status: number): string {
    switch (status) {
      case 0:
        return "border-border bg-muted/50 text-muted-foreground";
      case 1:
        return "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400";
      case 2:
        return "border-blue-500/30 bg-blue-500/10 text-blue-700 dark:text-blue-400";
      case 3:
        return "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400";
      case 4:
        return "border-emerald-600/30 bg-emerald-600/10 text-emerald-800 dark:text-emerald-300";
      case 5:
        return "border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-400";
      default:
        return "border-border bg-muted/50 text-muted-foreground";
    }
  }

  function formatDate(iso: string) {
    try {
      return new Date(iso).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch { return iso; }
  }

  function formatApprovalApprovers(a: Approval): string {
    if (a.approverUserIds?.length) return a.approverUserIds.join(', ');
    if (a.roleLabel || a.role) return a.roleLabel || a.role;
    return '—';
  }

  function parseChanges(body: string): { column: string; label: string; oldValue: string; newValue: string }[] {
    try {
      return JSON.parse(body)?.changes ?? [];
    } catch { return []; }
  }

  function requestBodyMode(body: string): "create" | "edit" | null {
    try {
      const m = JSON.parse(body)?.mode;
      if (m === "create") return "create";
      if (m === "edit") return "edit";
      return null;
    } catch {
      return null;
    }
  }

  /** venUI Select options for template `select` fields (string option lists). */
  function selectOptionsFromStrings(opts: string[] | undefined): { value: string; label: string }[] {
    return (opts ?? []).map((opt) => ({ value: opt, label: opt || "(blank)" }));
  }

  onMount(() => {
    fetchRequestTypes();
  });

  $effect(() => {
    if (activeTab === 'my') fetchMyRequests();
  });
</script>

<svelte:head>
  <title>Navision Edit Requests</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
  <PageHeading backHref="/" icon="file-pen">
    {#snippet title()}Navision Edit Requests{/snippet}
  </PageHeading>

  <Tabs bind:value={activeTab} class="flex min-h-0 flex-1 flex-col">
    <TabsList
      class="bg-card sticky top-0 z-20 inline-flex h-11 w-full items-center justify-start gap-1 rounded-none border-b border-border p-0 px-4 md:px-6"
    >
      <TabsTrigger
        value="new"
        class="ring-offset-background focus-visible:ring-ring data-[state=active]:bg-background data-[state=active]:text-foreground inline-flex items-center gap-2 rounded-none border-b-2 border-transparent px-4 py-3 text-sm font-medium text-muted-foreground transition-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 data-[state=active]:border-primary data-[state=active]:text-foreground"
      >
        <Icon name="circle-plus" class="size-4" />
        New Request
      </TabsTrigger>
      <TabsTrigger
        value="my"
        class="ring-offset-background focus-visible:ring-ring data-[state=active]:bg-background data-[state=active]:text-foreground inline-flex items-center gap-2 rounded-none border-b-2 border-transparent px-4 py-3 text-sm font-medium text-muted-foreground transition-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 data-[state=active]:border-primary data-[state=active]:text-foreground"
      >
        <Icon name="clock" class="size-4" />
        My Requests
      </TabsTrigger>
    </TabsList>

    <div class="flex-1 overflow-y-auto p-4 md:p-6">
      <TabsContent value="new" class="mt-0 outline-none">
        <div class="mx-auto w-full max-w-3xl space-y-6">
          <Card class="gap-4 py-5">
            <CardHeader class="gap-2 pb-0">
              <CardTitle class="flex items-center gap-3 text-base font-semibold">
                <span
                  class="bg-primary text-primary-foreground flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                  >1</span
                >
                Select Request Type
              </CardTitle>
            </CardHeader>
            <CardContent class="pt-2">
              {#if loading}
                <div class="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-3">
                  {#each { length: 3 } as _}
                    <Skeleton class="h-16 rounded-xl" />
                  {/each}
                </div>
              {:else if requestTypes.length === 0}
                <EmptyState
                  icon="inbox"
                  title="No request types"
                  description="No request types are configured. Please contact your administrator."
                />
              {:else}
                <div class="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-3">
                  {#each requestTypes as rt (rt.id)}
                    <Button
                      variant={selectedTypeId === rt.id ? "default" : "outline"}
                      class={cn(
                        "h-auto min-h-[4.5rem] w-full flex-col items-stretch justify-start gap-2 p-4 text-left transition-all",
                        selectedTypeId === rt.id && "ring-2 ring-primary/20"
                      )}
                      onclick={() => selectType(rt.id)}
                    >
                      <div class="flex w-full items-start gap-3">
                        <div
                          class="bg-muted text-primary flex size-9 shrink-0 items-center justify-center rounded-lg"
                        >
                          <Icon name={rt.icon || "file-text"} class="size-[1.1rem]" />
                        </div>
                        <div class="min-w-0 flex-1">
                          <div class="text-sm font-semibold leading-tight">{rt.name}</div>
                          {#if rt.description}
                            <div class="text-muted-foreground mt-1 text-xs leading-snug">{rt.description}</div>
                          {/if}
                        </div>
                        {#if selectedTypeId === rt.id}
                          <Icon name="circle-check" class="text-primary size-5 shrink-0" />
                        {/if}
                      </div>
                    </Button>
                  {/each}
                </div>
              {/if}
            </CardContent>
          </Card>

          {#if selectedTypeId && templateConfig?.allowNewRecordCreate}
            <div transition:slide={{ duration: 200 }}>
              <Card class="gap-4 py-5">
                <CardHeader class="gap-2 pb-0">
                  <CardTitle class="flex items-center gap-3 text-base font-semibold">
                    <span
                      class="bg-primary text-primary-foreground flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                      >2</span
                    >
                    Request type
                  </CardTitle>
                </CardHeader>
                <CardContent class="flex flex-wrap gap-2 pt-2">
                  <Button
                    variant={workflowMode === "edit" ? "default" : "outline"}
                    size="sm"
                    class="gap-2"
                    onclick={() => setWorkflowMode("edit")}
                  >
                    <Icon name="pencil" class="size-4" />
                    Edit existing record
                  </Button>
                  <Button
                    variant={workflowMode === "create" ? "default" : "outline"}
                    size="sm"
                    class="gap-2"
                    onclick={() => setWorkflowMode("create")}
                  >
                    <Icon name="copy" class="size-4" />
                    New record (copy optional)
                  </Button>
                  <p class="text-muted-foreground w-full text-xs leading-snug">
                    New record: enter a new primary key and field values. Optionally search and pick an existing row to copy
                    values from (e.g. new Item or BOM based on another).
                  </p>
                </CardContent>
              </Card>
            </div>
          {/if}

          {#if selectedTypeId && (workflowMode === "edit" || !templateConfig?.allowNewRecordCreate)}
            <div transition:slide={{ duration: 200 }}>
            <Card class="gap-4 py-5">
              <CardHeader class="gap-2 pb-0">
                <CardTitle class="flex items-center gap-3 text-base font-semibold">
                  <span
                    class="bg-primary text-primary-foreground flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                    >{templateConfig?.allowNewRecordCreate ? "3" : "2"}</span
                  >
                  Find record to edit
                </CardTitle>
              </CardHeader>
              <CardContent class="space-y-3 pt-2">
                <div class="relative">
                  <Icon
                    name="search"
                    class="text-muted-foreground pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 opacity-60"
                  />
                  <Input
                    class="pl-9 pr-9"
                    type="search"
                    autocomplete="off"
                    autocorrect="off"
                    spellcheck={false}
                    placeholder="Type to search…"
                    aria-autocomplete="list"
                    aria-busy={searchLoading}
                    bind:value={searchTerm}
                    oninput={scheduleLiveSearch}
                    onfocus={onSearchFocus}
                  />
                  {#if searchLoading}
                    <Loader2 class="text-primary absolute right-3 top-1/2 size-4 -translate-y-1/2 animate-spin" />
                  {/if}
                </div>
                <p class="text-muted-foreground text-xs leading-snug">
                  Results update as you type (up to {LOOKUP_TAKE} rows); no search button needed.
                </p>
                {#if selectedTypeId && templateConfig}
                  {@const searchableCols = getSearchableColumnsDisplay()}
                  {#if searchableCols.length > 0}
                    <div class="flex flex-wrap items-center gap-x-2 gap-y-1.5">
                      <span class="text-muted-foreground text-[0.65rem] font-semibold uppercase tracking-wide"
                        >Search matches</span
                      >
                      <div class="flex flex-wrap gap-1">
                        {#each searchableCols as col (col)}
                          <Badge variant="secondary" class="font-normal">{col}</Badge>
                        {/each}
                      </div>
                    </div>
                  {/if}
                {/if}

                {#if searchResults.length > 0}
                  <div
                    class="bg-background max-h-[250px] overflow-y-auto rounded-md border"
                    transition:slide={{ duration: 150 }}
                  >
                    {#each searchResults as row}
                      {@const rt = requestTypes.find((t) => t.id === selectedTypeId)}
                      {@const rowKvs = orderLookupRowKvs(row, templateConfig?.displayColumns, rt?.navPrimaryKeyColumn)}
                      <Button
                        variant="ghost"
                        class="hover:bg-muted h-auto min-h-0 w-full flex-wrap justify-start gap-x-4 gap-y-2 rounded-none border-b px-3 py-2.5 text-left last:border-b-0"
                        onclick={() => selectRecord(row)}
                      >
                        {#each rowKvs as kv}
                          <span class="flex flex-col gap-0.5">
                            <span
                              class="text-muted-foreground text-[0.6rem] font-semibold uppercase tracking-wide"
                              >{kv.key}</span
                            >
                            <span class="text-sm font-semibold"
                              >{formatNavDisplayColumnValue(
                                kv.value,
                                formatForDisplayColumnKey(kv.key, templateConfig?.displayColumns ?? [])
                              )}</span
                            >
                          </span>
                        {/each}
                      </Button>
                    {/each}
                  </div>
                {/if}

                {#if recordLoading && workflowMode === "edit"}
                  <div class="text-muted-foreground flex items-center gap-2 text-sm">
                    <Loader2 class="size-4 animate-spin" />
                    Loading record…
                  </div>
                {/if}
              </CardContent>
            </Card>
            </div>
          {/if}

          {#if selectedTypeId && workflowMode === "create" && templateConfig?.allowNewRecordCreate}
            <div transition:slide={{ duration: 200 }}>
              <Card class="gap-4 py-5">
                <CardHeader class="gap-2 pb-0">
                  <CardTitle class="flex items-center gap-3 text-base font-semibold">
                    <span
                      class="bg-primary text-primary-foreground flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                      >3</span
                    >
                    New record — copy from (optional) &amp; new key
                    </CardTitle>
                </CardHeader>
                <CardContent class="space-y-4 pt-2">
                  <div class="space-y-2">
                    <Label class="text-muted-foreground text-[0.7rem] font-bold uppercase tracking-wide"
                      >Copy field values from existing row (optional)</Label
                    >
                    <div class="relative">
                      <Icon
                        name="search"
                        class="text-muted-foreground pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 opacity-60"
                      />
                      <Input
                        class="pl-9 pr-9"
                        type="search"
                        autocomplete="off"
                        bind:value={copySearchTerm}
                        oninput={scheduleCopySearch}
                        onfocus={onCopySearchFocus}
                        placeholder="Search an existing row to copy…"
                      />
                      {#if copySearchLoading}
                        <Loader2 class="text-primary absolute right-3 top-1/2 size-4 -translate-y-1/2 animate-spin" />
                      {/if}
                    </div>
                    {#if copySearchResults.length > 0}
                      <div class="bg-background max-h-[200px] overflow-y-auto rounded-md border">
                        {#each copySearchResults as row}
                          {@const rt = requestTypes.find((t) => t.id === selectedTypeId)}
                          {@const rowKvs = orderLookupRowKvs(row, templateConfig?.displayColumns, rt?.navPrimaryKeyColumn)}
                          <Button
                            variant="ghost"
                            class="hover:bg-muted h-auto min-h-0 w-full flex-wrap justify-start gap-x-4 gap-y-2 rounded-none border-b px-3 py-2.5 text-left last:border-b-0"
                            onclick={() => selectRecordFromCopy(row)}
                          >
                            {#each rowKvs as kv}
                              <span class="flex flex-col gap-0.5">
                                <span
                                  class="text-muted-foreground text-[0.6rem] font-semibold uppercase tracking-wide"
                                  >{kv.key}</span
                                >
                                <span class="text-sm font-semibold"
                                  >{formatNavDisplayColumnValue(
                                    kv.value,
                                    formatForDisplayColumnKey(kv.key, templateConfig?.displayColumns ?? [])
                                  )}</span
                                >
                              </span>
                            {/each}
                          </Button>
                        {/each}
                      </div>
                    {/if}
                    {#if sourceRecordKey}
                      <p class="text-muted-foreground text-xs">
                        Copying from: <span class="text-foreground font-semibold">{sourceRecordKey}</span>
                      </p>
                    {/if}
                  </div>
                  <div class="space-y-2">
                    <Label for="nav-new-pk" class="text-muted-foreground text-[0.7rem] font-bold uppercase tracking-wide"
                      >New {primaryKeyColumn() || "primary key"} (required)</Label
                    >
                    <Input
                      id="nav-new-pk"
                      bind:value={newRecordKey}
                      placeholder="Enter the new record key…"
                      autocomplete="off"
                    />
                  </div>
                  {#if recordLoading && workflowMode === "create"}
                    <div class="text-muted-foreground flex items-center gap-2 text-sm">
                      <Loader2 class="size-4 animate-spin" />
                      Loading row to copy…
                    </div>
                  {/if}
                </CardContent>
              </Card>
            </div>
          {/if}

          {#if templateConfig && selectedTypeId && (workflowMode === "create" || (workflowMode === "edit" && selectedRecord))}
            <div transition:slide={{ duration: 200 }}>
            <Card class="gap-4 py-5">
              <CardHeader class="gap-2 pb-0">
                <CardTitle class="flex items-center gap-3 text-base font-semibold">
                  <span
                    class="bg-primary text-primary-foreground flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                    >{templateConfig.allowNewRecordCreate ? "4" : "3"}</span
                  >
                  {workflowMode === "create" ? "Proposed new record" : "Modify fields"}
                </CardTitle>
              </CardHeader>
              <CardContent class="space-y-4 pt-2">
                {#if workflowMode === "create"}
                  <p class="text-muted-foreground text-xs leading-snug">
                    Set the new key above, then adjust fields. “Current” shows the copied row when you copied from an existing
                    record; otherwise it is empty.
                  </p>
                {/if}
                <div class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-4">
                  {#each templateConfig.fields ?? [] as field (field.column)}
                    {@const masterFieldType = parseNavEditFieldMasterType(field.type)}
                    {@const curFromNav = selectedRecord?.[field.column] ?? ""}
                    <div class="flex flex-col gap-2">
                      <Label class="text-muted-foreground text-[0.7rem] font-bold uppercase tracking-wide"
                        >{field.label}</Label
                      >
                      <div class="text-muted-foreground flex flex-wrap gap-1 text-xs">
                        <span class="font-medium">Current:</span>
                        <span class="text-foreground font-semibold"
                          >{field.type === "date"
                            ? formatNavDisplayColumnValue(curFromNav, "date")
                            : field.type === "number"
                              ? formatNavDisplayColumnValue(curFromNav, "number")
                              : curFromNav || "—"}</span
                        >
                      </div>
                      {#if field.type === "select" && field.options}
                        <VenSelect
                          options={selectOptionsFromStrings(field.options)}
                          valueKey="value"
                          labelKey="label"
                          bind:value={editValues[field.column]}
                          placeholder="Select…"
                          searchPlaceholder="Search options…"
                          class="w-full"
                        />
                      {:else if masterFieldType}
                        <div class="min-w-0">
                          <NavEditMasterField
                            masterType={masterFieldType}
                            bind:value={editValues[field.column]}
                            placeholder="Search…"
                          />
                        </div>
                      {:else if field.type === "date"}
                        <DatePicker
                          valueType="text"
                          valueFormat="yyyy-MM-dd"
                          placeholder="Select date…"
                          value={editValues[field.column] ? editValues[field.column] : undefined}
                          onValueChange={(v) => {
                            editValues[field.column] = typeof v === "string" ? v : "";
                          }}
                        />
                      {:else}
                        <Input
                          type={field.type === "number" ? "number" : "text"}
                          bind:value={editValues[field.column]}
                          placeholder="New value…"
                        />
                      {/if}
                    </div>
                  {/each}
                </div>

                {#if templateConfig.approvals && templateConfig.approvals.length > 0}
                  <div
                    class="border-blue-500/20 bg-blue-500/8 text-blue-700 dark:text-blue-400 flex items-start gap-2 rounded-lg border px-3 py-2.5 text-sm"
                  >
                    <Icon name="shield-check" class="mt-0.5 size-4 shrink-0" />
                    <span
                      >This request requires {templateConfig.approvals.length} approval{templateConfig
                        .approvals.length > 1
                        ? "s"
                        : ""} before processing.</span
                    >
                  </div>
                {/if}

                <div class="space-y-2">
                  <Label for="nav-edit-remark" class="text-muted-foreground text-[0.7rem] font-bold uppercase tracking-wide"
                    >Remark (optional)</Label
                  >
                  <Textarea
                    id="nav-edit-remark"
                    bind:value={remark}
                    placeholder="Add a note for the admin…"
                    rows={2}
                    class="min-h-[4rem]"
                  />
                </div>

                <div class="flex justify-end pt-1">
                  <Button onclick={submitRequest} disabled={submitting} class="gap-2">
                    {#if submitting}
                      <Loader2 class="size-4 animate-spin" />
                      Submitting…
                    {:else}
                      <Icon name="send" class="size-4" />
                      Submit Request
                    {/if}
                  </Button>
                </div>
              </CardContent>
            </Card>
            </div>
          {/if}
        </div>
      </TabsContent>

      <TabsContent value="my" class="mt-0 outline-none">
        <div class="mx-auto w-full max-w-3xl px-3 pb-8 pt-1 sm:px-4 md:px-0 md:pb-10">
          {#if myRequestsLoading}
            <div class="flex flex-col gap-3">
              {#each { length: 4 } as _}
                <Skeleton class="min-h-[5.5rem] w-full rounded-2xl sm:min-h-[4.75rem]" />
              {/each}
            </div>
          {:else if myRequests.length === 0}
            <EmptyState
              icon="inbox"
              title="No requests yet"
              description="You have not submitted any Navision edit requests. Create one from the New Request tab."
            />
          {:else}
            <ul class="flex flex-col gap-3 sm:gap-3.5" role="list">
              {#each myRequests as req (req.id)}
                <li>
                  <Card
                    class={cn(
                      "overflow-hidden rounded-2xl border border-border/80 py-0 shadow-sm transition-shadow",
                      expandedRequestId === req.id && "ring-2 ring-primary/25 shadow-md"
                    )}
                  >
                    <button
                      type="button"
                      class={cn(
                        "touch-manipulation flex w-full gap-3 p-3.5 text-left sm:gap-4 sm:p-4",
                        "hover:bg-muted/40 active:bg-muted/60",
                        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background",
                        expandedRequestId === req.id && "bg-muted/25"
                      )}
                      aria-expanded={expandedRequestId === req.id}
                      onclick={() =>
                        (expandedRequestId = expandedRequestId === req.id ? null : req.id)}
                    >
                      <div
                        class="bg-muted text-primary flex size-11 shrink-0 items-center justify-center rounded-xl sm:size-10"
                      >
                        <Icon name={req.requestTypeIcon || "file-text"} class="size-[1.35rem] sm:size-5" />
                      </div>
                      <div class="min-w-0 flex-1 space-y-2">
                        <div
                          class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between sm:gap-3"
                        >
                          <p class="text-foreground text-[0.9375rem] font-semibold leading-snug sm:text-sm">
                            {req.requestTypeName}
                          </p>
                          <div
                            class="flex flex-wrap items-center gap-x-2 gap-y-1.5 sm:shrink-0 sm:justify-end"
                          >
                            <Badge
                              variant="outline"
                              class={cn(
                                "whitespace-nowrap px-2 py-0.5 text-[0.7rem] font-semibold sm:text-xs",
                                statusBadgeClass(req.status)
                              )}
                            >
                              {req.statusLabel}
                            </Badge>
                            <time
                              class="text-muted-foreground text-[0.7rem] tabular-nums sm:text-xs"
                              datetime={req.createdAt}
                            >
                              {formatDate(req.createdAt)}
                            </time>
                          </div>
                        </div>
                        <div
                          class="text-muted-foreground flex flex-wrap items-baseline gap-x-2 gap-y-1 text-xs leading-relaxed"
                        >
                          <span class="min-w-0 break-all">
                            <span class="text-muted-foreground/90">Record:</span>
                            <span class="text-foreground ml-1 font-medium">{req.recordKey}</span>
                          </span>
                          {#if requestBodyMode(req.requestBody) === "create"}
                            <Badge variant="secondary" class="shrink-0 text-[0.65rem] font-semibold"
                              >New record</Badge
                            >
                          {/if}
                        </div>
                      </div>
                      <Icon
                        name="chevron-down"
                        class={cn(
                          "text-muted-foreground mt-0.5 size-5 shrink-0 transition-transform duration-200",
                          expandedRequestId === req.id && "rotate-180"
                        )}
                        aria-hidden="true"
                      />
                    </button>

                    {#if expandedRequestId === req.id}
                      <div transition:slide={{ duration: 200 }}>
                        <CardContent
                          class="bg-muted/20 space-y-4 border-t border-border/80 px-3 py-4 sm:px-4"
                        >
                          <!-- Mobile: stacked change cards -->
                          <div class="space-y-2.5 md:hidden">
                            {#each parseChanges(req.requestBody) as change}
                              <div
                                class="bg-card space-y-1.5 rounded-xl border border-border/70 p-3 text-sm shadow-sm"
                              >
                                <div
                                  class="text-muted-foreground text-[0.65rem] font-bold uppercase tracking-wide"
                                >
                                  {change.label || change.column}
                                </div>
                                <div class="text-muted-foreground text-xs line-through">
                                  {change.oldValue || "—"}
                                </div>
                                <div class="text-primary text-sm font-semibold leading-snug">
                                  {change.newValue || "—"}
                                </div>
                              </div>
                            {/each}
                          </div>
                          <!-- md+: table -->
                          <div class="hidden overflow-x-auto rounded-lg border border-border/80 md:block">
                            <Table>
                              <TableHeader>
                                <TableRow>
                                  <TableHead class="min-w-[6rem]">Field</TableHead>
                                  <TableHead class="min-w-[6rem]">Old Value</TableHead>
                                  <TableHead class="min-w-[6rem]">New Value</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {#each parseChanges(req.requestBody) as change}
                                  <TableRow>
                                    <TableCell class="font-semibold">{change.label || change.column}</TableCell>
                                    <TableCell class="text-muted-foreground line-through"
                                      >{change.oldValue || "—"}</TableCell
                                    >
                                    <TableCell class="text-primary font-semibold">{change.newValue || "—"}</TableCell>
                                  </TableRow>
                                {/each}
                              </TableBody>
                            </Table>
                          </div>
                          {#if req.remark}
                            <div class="bg-muted rounded-xl px-3 py-2.5 text-sm leading-relaxed">
                              <strong class="text-foreground">Your remark:</strong>
                              {req.remark}
                            </div>
                          {/if}
                          {#if req.adminRemark}
                            <div
                              class="rounded-xl border border-amber-500/25 bg-amber-500/10 px-3 py-2.5 text-sm leading-relaxed text-amber-950 dark:text-amber-100"
                            >
                              <strong>Admin remark:</strong>
                              {req.adminRemark}
                            </div>
                          {/if}
                          {#if req.approvals.length > 0}
                            <div class="space-y-2.5">
                              <div
                                class="text-muted-foreground text-[0.65rem] font-bold uppercase tracking-wide"
                              >
                                Approvals
                              </div>
                              {#each req.approvals as a}
                                <div class="flex flex-wrap items-center gap-2 text-sm leading-snug">
                                  <span
                                    class="size-2 shrink-0 rounded-full {a.status === 1
                                      ? 'bg-emerald-500'
                                      : a.status === 2
                                        ? 'bg-rose-500'
                                        : 'bg-slate-400'}"
                                  ></span>
                                  <span class="font-semibold">{formatApprovalApprovers(a)} (Level {a.level})</span>
                                  <span class="text-muted-foreground">{a.statusLabel}</span>
                                  {#if a.approvedBy}
                                    <span class="text-muted-foreground text-xs">by {a.approvedBy}</span>
                                  {/if}
                                </div>
                              {/each}
                            </div>
                          {/if}
                          {#if canResendNotifications(req)}
                            <div
                              class="flex flex-col gap-3 border-t border-border/80 pt-3 sm:flex-row sm:items-center sm:justify-between"
                            >
                              <p class="text-muted-foreground max-w-md text-xs leading-relaxed">
                                Did IT or approvers miss the alert? Send the same notifications again.
                              </p>
                              <Button
                                variant="outline"
                                size="sm"
                                class="w-full shrink-0 gap-2 sm:w-auto"
                                disabled={resendingRequestId === req.id}
                                onclick={() => resendNotifications(req)}
                              >
                                {#if resendingRequestId === req.id}
                                  <Loader2 class="size-4 animate-spin" />
                                  Sending…
                                {:else}
                                  <Icon name="bell" class="size-4" />
                                  Resend notifications
                                {/if}
                              </Button>
                            </div>
                          {/if}
                        </CardContent>
                      </div>
                    {/if}
                  </Card>
                </li>
              {/each}
            </ul>
          {/if}
        </div>
      </TabsContent>
    </div>
  </Tabs>
</div>
