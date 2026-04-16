<script lang="ts">
  import { Icon } from "$lib/components/venUI/icon";
  import { Select } from "$lib/components/venUI/select";
  import NavUserPickField from "$lib/components/navision-edits/NavUserPickField.svelte";
  import {
    CONNECTOR_PARAM_ROWS,
    type NavEditFieldsTemplate,
    type NavEditConnectorProcess,
    type NavEditApprovalStep,
    type NavEditConditionDef,
    type NavEditFieldDef,
    type ConditionOp,
    type NavEditDisplayColumnFormat,
    type NavEditLookupFilterCondition,
    type NavEditLookupFilterOp,
    type NavEditConnectorParamRow,
  } from "$lib/navision-edits/templateTypes";
  import { navEditFieldTypeOptionsWith } from "$lib/navision-edits/nav-field-types";

  const CONNECTOR_OPTIONS: { value: NavEditConnectorProcess; label: string }[] = [
    { value: "none", label: "None — mark processed only (no NAV call)" },
    { value: "reqCustEdit", label: "ReqCustEdit (customer, dealer, area)" },
    { value: "reqUserSetup", label: "ReqUserSetup (user, RC, date range)" },
    { value: "reqGlEntry", label: "ReqGLEntry (entry, G/L, amount, …)" },
  ];

  const DISPLAY_FORMAT_OPTIONS: { value: NavEditDisplayColumnFormat; label: string }[] = [
    { value: "", label: "Default (text)" },
    { value: "date", label: "Date (lookup display)" },
    { value: "number", label: "Number (locale)" },
  ];

  let {
    model = $bindable(),
    columnOptions = [],
  }: { model: NavEditFieldsTemplate; columnOptions?: string[] } = $props();

  /** Merge DB column names with any existing template value so legacy edits still show in the list. */
  function columnOptsWith(current: unknown): string[] {
    const s = new Set(columnOptions);
    let t = "";
    if (typeof current === "string") t = current.trim();
    else if (current && typeof current === "object" && "column" in (current as object)) {
      t = String((current as { column?: unknown }).column ?? "").trim();
    } else if (current != null && current !== "") t = String(current).trim();
    if (t) s.add(t);
    return [...s].sort((a, b) => a.localeCompare(b));
  }

  const condOps: { value: ConditionOp; label: string }[] = [
    { value: "eq", label: "equals (eq)" },
    { value: "neq", label: "not equals (neq)" },
    { value: "in", label: "in list (in)" },
    { value: "nin", label: "not in list (nin)" },
  ];

  const lookupFilterOps: { value: NavEditLookupFilterOp; label: string }[] = [
    { value: "eq", label: "Equals (eq)" },
    { value: "neq", label: "Not equals (neq)" },
    { value: "contains", label: "Contains (contains)" },
    { value: "notcontains", label: "Does not contain (notcontains)" },
    { value: "starts", label: "Starts with (starts)" },
    { value: "ends", label: "Ends with (ends)" },
    { value: "in", label: "In list (in)" },
    { value: "nin", label: "Not in list (nin)" },
    { value: "isnull", label: "Is null (isnull)" },
    { value: "isnotnull", label: "Is not null (isnotnull)" },
  ];

  function splitIds(s: string): string[] {
    return s
      .split(/[,;\n]+/)
      .map(x => x.trim())
      .filter(Boolean);
  }

  function patchModel(updater: (m: NavEditFieldsTemplate) => void) {
    updater(model);
    model = { ...model };
  }

  /** Legacy templates may still have `displayColumns: ["Col1", …]`; normalize to `{ column, format }` for binds. */
  $effect(() => {
    const cols = model.displayColumns;
    if (!cols?.length) return;
    if (!cols.some((c) => typeof c === "string")) return;
    patchModel((m) => {
      m.displayColumns = m.displayColumns.map((c) =>
        typeof c === "string" ? { column: c, format: "" as NavEditDisplayColumnFormat } : c
      );
    });
  });

  /** Ensure `searchColumns` exists when editing legacy JSON without this key. */
  $effect(() => {
    if (!Array.isArray(model.searchColumns)) {
      patchModel((m) => {
        m.searchColumns = [];
      });
    }
  });

  /** Ensure `lookupFilters` exists for legacy templates. */
  $effect(() => {
    if (!Array.isArray(model.lookupFilters)) {
      patchModel((m) => {
        m.lookupFilters = [];
      });
    }
  });

  /** Legacy templates without `allowNewRecordCreate`. */
  $effect(() => {
    if (typeof model.allowNewRecordCreate !== "boolean") {
      patchModel((m) => {
        m.allowNewRecordCreate = false;
      });
    }
  });

  /** Legacy templates without connector fields. */
  $effect(() => {
    const cp = model.connectorProcess;
    if (cp !== "none" && cp !== "reqCustEdit" && cp !== "reqUserSetup" && cp !== "reqGlEntry") {
      patchModel((m) => {
        m.connectorProcess = "none";
      });
    }
    if (!model.connectorParamColumns || typeof model.connectorParamColumns !== "object") {
      patchModel((m) => {
        m.connectorParamColumns = {};
      });
    }
  });

  let connectorParamDraft = $state("{}");
  let connectorParamFocused = $state(false);
  /** Form rows vs raw JSON for `connectorParamColumns`. */
  let connectorParamEditorMode = $state<"form" | "json">("form");

  const connectorParamRowList = $derived.by((): NavEditConnectorParamRow[] => {
    const p = model.connectorProcess;
    if (p === "none") return [];
    return CONNECTOR_PARAM_ROWS[p];
  });

  $effect(() => {
    if (!connectorParamFocused) {
      connectorParamDraft = JSON.stringify(model.connectorParamColumns ?? {}, null, 2);
    }
  });

  function onConnectorParamFocus() {
    connectorParamFocused = true;
    connectorParamDraft = JSON.stringify(model.connectorParamColumns ?? {}, null, 2);
  }

  function onConnectorParamBlur() {
    connectorParamFocused = false;
    try {
      const raw = connectorParamDraft.trim() || "{}";
      const p = JSON.parse(raw) as unknown;
      if (typeof p !== "object" || p === null || Array.isArray(p)) {
        connectorParamDraft = JSON.stringify(model.connectorParamColumns ?? {}, null, 2);
        return;
      }
      patchModel((m) => {
        const next: Record<string, string> = {};
        for (const [k, v] of Object.entries(p as Record<string, unknown>)) {
          if (typeof v === "string" && v.trim()) next[k] = v.trim();
        }
        m.connectorParamColumns = next;
      });
    } catch {
      connectorParamDraft = JSON.stringify(model.connectorParamColumns ?? {}, null, 2);
    }
  }

  function patchConnectorParam(key: string, navColumn: string) {
    patchModel((m) => {
      const next = { ...m.connectorParamColumns };
      const t = navColumn.trim();
      if (!t) delete next[key];
      else next[key] = t;
      m.connectorParamColumns = next;
    });
  }

  function onConnectorColumnSelect(key: string, item: unknown) {
    if (item == null) {
      patchConnectorParam(key, "");
      return;
    }
    const v =
      typeof item === "string"
        ? item
        : String((item as Record<string, unknown>).value ?? "");
    patchConnectorParam(key, v);
  }

  function addDisplayColumn() {
    patchModel(m => {
      m.displayColumns = [...m.displayColumns, { column: "", format: "" }];
    });
  }

  function removeDisplayColumn(i: number) {
    patchModel(m => {
      m.displayColumns = m.displayColumns.filter((_, j) => j !== i);
    });
  }

  function addSearchColumn() {
    patchModel((m) => {
      m.searchColumns = [...m.searchColumns, ""];
    });
  }

  function removeSearchColumn(i: number) {
    patchModel((m) => {
      m.searchColumns = m.searchColumns.filter((_, j) => j !== i);
    });
  }

  function addLookupFilter() {
    patchModel((m) => {
      m.lookupFilters = [
        ...m.lookupFilters,
        { column: "", op: "eq", value: "", values: [] } satisfies NavEditLookupFilterCondition,
      ];
    });
  }

  function removeLookupFilter(i: number) {
    patchModel((m) => {
      m.lookupFilters = m.lookupFilters.filter((_, j) => j !== i);
    });
  }

  function lookupFilterValuesText(f: NavEditLookupFilterCondition): string {
    return (f.values ?? []).join(", ");
  }

  function setLookupFilterValues(fi: number, text: string) {
    patchModel((m) => {
      const f = m.lookupFilters[fi];
      if (!f) return;
      f.values = splitIds(text);
    });
  }

  function addField() {
    patchModel(m => {
      m.fields = [...m.fields, { column: "", label: "", type: "text" }];
    });
  }

  function removeField(i: number) {
    patchModel(m => {
      if (m.fields.length <= 1) return;
      m.fields = m.fields.filter((_, j) => j !== i);
    });
  }

  function setFieldOptions(f: NavEditFieldDef, optsText: string) {
    const opts = splitIds(optsText);
    f.options = opts.length ? opts : undefined;
  }

  function addApprovalStep() {
    patchModel(m => {
      m.approvals = [...m.approvals, { userIds: [], when: [] }];
    });
  }

  function removeApprovalStep(i: number) {
    patchModel(m => {
      m.approvals = m.approvals.filter((_, j) => j !== i);
    });
  }

  function addCondition(step: NavEditApprovalStep) {
    step.when = [...step.when, { column: "", op: "eq", value: "", values: [] }];
    model = { ...model };
  }

  function removeCondition(step: NavEditApprovalStep, j: number) {
    step.when = step.when.filter((_, k) => k !== j);
    model = { ...model };
  }

  function conditionValuesText(c: NavEditConditionDef): string {
    return (c.values ?? []).join(", ");
  }

  function setConditionValues(c: NavEditConditionDef, text: string) {
    c.values = splitIds(text);
    model = { ...model };
  }
</script>

<div class="ndt">

  <section class="ndt-section">
    <h4 class="ndt-h">IT admin user ID</h4>
    <NavUserPickField bind:value={model.itAdminUserId} placeholder="Choose IT admin (Nav user)…" />
  </section>

  <section class="ndt-section">
    <h4 class="ndt-h">NAV processing (admin “mark processed”)</h4>
    <p class="ndt-hint">
      When not “none”, the server calls the matching NAV WebServe Req* method via Connector. Required values must appear in the user’s submitted field changes (or map column names below).
    </p>
    <div class="ndt-select-ven ndt-select-ven--connector">
      <Select
        options={CONNECTOR_OPTIONS}
        valueKey="value"
        labelKey="label"
        bind:value={model.connectorProcess}
        placeholder="Connector…"
        searchPlaceholder="Search…"
        emptyText="No options"
      />
    </div>
    {#if model.connectorProcess !== "none"}
      <div class="ndt-connector-map-toolbar">
        <span class="ndt-connector-map-toolbar-title" id="connector-map-heading">Parameter mapping</span>
        <div class="ndt-connector-map-mode" role="group" aria-label="Connector parameter editor mode">
          <button
            type="button"
            class="ndt-connector-mode-btn"
            class:ndt-connector-mode-btn--active={connectorParamEditorMode === "form"}
            onclick={() => {
              connectorParamEditorMode = "form";
            }}
          >
            Form
          </button>
          <button
            type="button"
            class="ndt-connector-mode-btn"
            class:ndt-connector-mode-btn--active={connectorParamEditorMode === "json"}
            onclick={() => {
              connectorParamEditorMode = "json";
              connectorParamDraft = JSON.stringify(model.connectorParamColumns ?? {}, null, 2);
            }}
          >
            JSON
          </button>
        </div>
      </div>
      <p class="ndt-hint ndt-hint--sm" id="connector-map-desc">
        Map each <strong>Req JSON key</strong> (logical parameter) to the <strong>Nav column name</strong> that appears in submitted field changes. Leave empty to use the server’s default column guesses for that operation.
      </p>
      {#if connectorParamEditorMode === "form"}
        <div class="ndt-connector-map" role="group" aria-labelledby="connector-map-heading connector-map-desc">
          <div class="ndt-connector-map-head" aria-hidden="true">
            <span>Req key</span>
            <span>Nav column</span>
          </div>
          {#each connectorParamRowList as row (row.key)}
            <div class="ndt-connector-map-row">
              <div class="ndt-connector-key-cell">
                <code class="ndt-connector-key-code">{row.key}</code>
                <span class="ndt-connector-key-label">{row.label}</span>
                {#if row.hint}
                  <span class="ndt-connector-key-hint">{row.hint}</span>
                {/if}
              </div>
              <div class="ndt-select-ven ndt-select-ven--connector-col">
                {#key `${row.key}:${model.connectorParamColumns[row.key] ?? ""}`}
                  <Select
                    options={columnOptsWith(model.connectorParamColumns[row.key] ?? "")}
                    value={model.connectorParamColumns[row.key] ?? ""}
                    onSelect={(item) => onConnectorColumnSelect(row.key, item)}
                    placeholder="Default (built-in names)"
                    clearable
                    searchPlaceholder="Search columns…"
                    emptyText="No matching columns"
                  />
                {/key}
              </div>
            </div>
          {/each}
        </div>
      {:else}
        <div class="ndt-connector-json-wrap" aria-labelledby="connector-map-heading">
          <div class="ndt-connector-json-caption">
            <code>connectorParamColumns</code>
            — object: logical key → Nav column name
          </div>
          <textarea
            class="ndt-connector-json"
            rows="6"
            spellcheck="false"
            bind:value={connectorParamDraft}
            onfocus={onConnectorParamFocus}
            onblur={onConnectorParamBlur}
            aria-label="connectorParamColumns JSON"
          ></textarea>
          <p class="ndt-hint ndt-hint--sm">
            Example: <code>{'{"dealerCode": "Dealer Code", "areaCode": "Area Code"}'}</code>
          </p>
        </div>
      {/if}
    {/if}
  </section>

  <section class="ndt-section">
    <div class="ndt-section-head">
      <h4 class="ndt-h">Display columns</h4>
      <button type="button" class="ndt-btn-add" onclick={addDisplayColumn}>
        <Icon name="plus" class="w-3.5 h-3.5" />
        Add column
      </button>
    </div>
    {#if columnOptions.length === 0}
      <p class="ndt-empty ndt-empty--sm">No column list yet — enter Nav Table above, then return here.</p>
    {/if}
    {#if model.displayColumns.length === 0}
      <p class="ndt-empty">No display columns. Add at least one for lookup, or leave empty to fall back to primary key only.</p>
    {/if}
    {#each model.displayColumns as _dc, i (i)}
      <div class="ndt-row ndt-row--display">
        <div class="ndt-select-ven">
          <Select
            options={columnOptsWith(model.displayColumns[i]?.column ?? "")}
            bind:value={model.displayColumns[i].column}
            placeholder="Select column…"
            clearable
            searchPlaceholder="Search columns…"
            emptyText="No matching columns"
          />
        </div>
        <div class="ndt-select-ven ndt-select-ven--format">
          <Select
            options={DISPLAY_FORMAT_OPTIONS}
            valueKey="value"
            labelKey="label"
            bind:value={model.displayColumns[i].format}
            placeholder="Format…"
            searchPlaceholder="Search…"
            emptyText="No formats"
          />
        </div>
        <button type="button" class="ndt-icon-btn" onclick={() => removeDisplayColumn(i)} title="Remove">
          <Icon name="trash-2" class="w-4 h-4" />
        </button>
      </div>
    {/each}
  </section>

  <section class="ndt-section">
    <div class="ndt-section-head">
      <h4 class="ndt-h">Search columns</h4>
      <button type="button" class="ndt-btn-add" onclick={addSearchColumn}>
        <Icon name="plus" class="w-3.5 h-3.5" />
        Add column
      </button>
    </div>
    <p class="ndt-hint">
      Find-record search matches any of these Nav columns (substring, case-insensitive). Leave empty to search the primary key column only.
    </p>
    {#if columnOptions.length === 0}
      <p class="ndt-empty ndt-empty--sm">No column list yet — enter Nav Table above, then return here.</p>
    {/if}
    {#if model.searchColumns.length === 0}
      <p class="ndt-empty">No extra search columns — lookup uses the request type primary key only.</p>
    {/if}
    <div class="ndt-search-grid">
      {#each model.searchColumns as _sc, i (i)}
        <div class="ndt-search-item">
          <div class="ndt-select-ven ndt-select-ven--search">
            <Select
              options={columnOptsWith(model.searchColumns[i] ?? "")}
              bind:value={model.searchColumns[i]}
              placeholder="Column to search…"
              clearable
              searchPlaceholder="Search columns…"
              emptyText="No matching columns"
            />
          </div>
          <button type="button" class="ndt-icon-btn" onclick={() => removeSearchColumn(i)} title="Remove">
            <Icon name="trash-2" class="w-4 h-4" />
          </button>
        </div>
      {/each}
    </div>
  </section>

  <section class="ndt-section">
    <div class="ndt-section-head">
      <h4 class="ndt-h">New record requests</h4>
    </div>
    <p class="ndt-hint">
      When enabled, users can request a <strong>new</strong> row (new primary key) and optionally load field values from an existing row as a starting point (e.g. new Item or BOM line copied from another).
    </p>
    <label class="ndt-checkbox-row">
      <input
        type="checkbox"
        checked={model.allowNewRecordCreate}
        onchange={(e) => {
          patchModel((m) => {
            m.allowNewRecordCreate = e.currentTarget.checked;
          });
        }}
      />
      <span>Allow new record requests (copy from existing)</span>
    </label>
  </section>

  <section class="ndt-section">
    <div class="ndt-section-head">
      <h4 class="ndt-h">Lookup table filters</h4>
      <button type="button" class="ndt-btn-add" onclick={addLookupFilter}>
        <Icon name="plus" class="w-3.5 h-3.5" />
        Add filter
      </button>
    </div>
    <p class="ndt-hint">
      Optional filters applied to every find-record query (combined with AND). User search is applied in addition to these.
    </p>
    {#if columnOptions.length === 0}
      <p class="ndt-empty ndt-empty--sm">No column list yet — enter Nav Table above, then return here.</p>
    {/if}
    {#if model.lookupFilters.length === 0}
      <p class="ndt-empty">No static filters — all rows (up to the search limit) are eligible unless the user types a search.</p>
    {/if}
    <div class="ndt-filter-table">
      <div class="ndt-filter-grid ndt-filter-head">
        <span>Column</span>
        <span>Operator</span>
        <span>Value</span>
        <span></span>
      </div>
      {#each model.lookupFilters as fil, fi (fi)}
        <div class="ndt-filter-grid">
          <div class="ndt-select-ven ndt-select-ven--filter-col">
            <Select
              options={columnOptsWith(fil.column)}
              bind:value={model.lookupFilters[fi].column}
              placeholder="Column…"
              clearable
              searchPlaceholder="Search columns…"
              emptyText="No matching columns"
            />
          </div>
          <select class="ndt-select ndt-select--filter-op" bind:value={model.lookupFilters[fi].op}>
            {#each lookupFilterOps as o}
              <option value={o.value}>{o.label}</option>
            {/each}
          </select>
          {#if fil.op === "in" || fil.op === "nin"}
            <input
              class="ndt-input ndt-input-grow"
              type="text"
              value={lookupFilterValuesText(fil)}
              oninput={(e) => setLookupFilterValues(fi, e.currentTarget.value)}
              placeholder="Values, comma-separated"
            />
          {:else if fil.op === "isnull" || fil.op === "isnotnull"}
            <span class="ndt-filter-no-value">—</span>
          {:else}
            <input class="ndt-input ndt-input-grow" type="text" bind:value={model.lookupFilters[fi].value} placeholder="Value" />
          {/if}
          <button type="button" class="ndt-icon-btn" onclick={() => removeLookupFilter(fi)} title="Remove">
            <Icon name="trash-2" class="w-4 h-4" />
          </button>
        </div>
      {/each}
    </div>
  </section>

  <section class="ndt-section">
    <div class="ndt-section-head">
      <h4 class="ndt-h">Editable fields</h4>
      <button type="button" class="ndt-btn-add" onclick={addField}>
        <Icon name="plus" class="w-3.5 h-3.5" />
        Add field
      </button>
    </div>
    <div class="ndt-field-grid ndt-field-head">
      <span>Column</span>
      <span>Label</span>
      <span>Type</span>
      <span>Options (static select)</span>
      <span></span>
    </div>
    {#each model.fields as f, i (i)}
      <div class="ndt-field-grid">
        <div class="ndt-select-ven ndt-select-ven--field">
          <Select
            options={columnOptsWith(f.column)}
            bind:value={f.column}
            placeholder="Column…"
            clearable
            searchPlaceholder="Search columns…"
            emptyText="No matching columns"
          />
        </div>
        <input class="ndt-input" type="text" bind:value={f.label} placeholder="Label" />
        <div class="ndt-select-ven ndt-select-ven--type">
          <Select
            options={navEditFieldTypeOptionsWith(f.type)}
            valueKey="value"
            labelKey="label"
            bind:value={f.type}
            placeholder="Field type…"
            searchPlaceholder="Search types…"
            emptyText="No types"
          />
        </div>
        <input
          class="ndt-input"
          type="text"
          disabled={f.type !== "select"}
          value={f.options?.join(", ") ?? ""}
          oninput={e => setFieldOptions(f, e.currentTarget.value)}
          placeholder="opt1, opt2"
        />
        <button type="button" class="ndt-icon-btn" onclick={() => removeField(i)} disabled={model.fields.length <= 1} title="Remove">
          <Icon name="trash-2" class="w-4 h-4" />
        </button>
      </div>
    {/each}
  </section>

  <section class="ndt-section">
    <div class="ndt-section-head">
      <h4 class="ndt-h">Approval steps</h4>
      <button type="button" class="ndt-btn-add" onclick={addApprovalStep}>
        <Icon name="plus" class="w-3.5 h-3.5" />
        Add step
      </button>
    </div>
    
    {#if model.approvals.length === 0}
      <p class="ndt-empty">No approval chain — requests go straight to Pending (no PendingApproval).</p>
    {/if}

    {#each model.approvals as step, si (si)}
      <div class="ndt-approval-card">
        <div class="ndt-approval-head">
          <span class="ndt-approval-title">Step {si + 1}</span>
          <button type="button" class="ndt-icon-btn" onclick={() => removeApprovalStep(si)} title="Remove step">
            <Icon name="trash-2" class="w-4 h-4" />
          </button>
        </div>
        <span class="ndt-label">Approvers (Nav users)</span>
        <NavUserPickField bind:value={step.userIds} multiple placeholder="Search and add approvers…" />

        <div class="ndt-when-head">
          <span class="ndt-when-title">When (all must match)</span>
          <button type="button" class="ndt-btn-add ndt-btn-add--sm" onclick={() => addCondition(step)}>
            <Icon name="plus" class="w-3 h-3" />
            Add condition
          </button>
        </div>
        {#if step.when.length === 0}
          <p class="ndt-empty ndt-empty--sm">No conditions — this step always applies.</p>
        {/if}
        {#each step.when as c, cj (cj)}
          <div class="ndt-cond">
            <div class="ndt-select-ven ndt-select-ven--cond">
              <Select
                options={columnOptsWith(c.column)}
                bind:value={c.column}
                placeholder="Column…"
                clearable
                searchPlaceholder="Search columns…"
                emptyText="No matching columns"
              />
            </div>
            <select class="ndt-select" bind:value={c.op}>
              {#each condOps as o}
                <option value={o.value}>{o.label}</option>
              {/each}
            </select>
            {#if c.op === "in" || c.op === "nin"}
              <input
                class="ndt-input ndt-input-grow"
                type="text"
                value={conditionValuesText(c)}
                oninput={e => setConditionValues(c, e.currentTarget.value)}
                placeholder="Values, comma-separated"
              />
            {:else}
              <input class="ndt-input ndt-input-grow" type="text" bind:value={c.value} placeholder="Value" />
            {/if}
            <button type="button" class="ndt-icon-btn" onclick={() => removeCondition(step, cj)} title="Remove">
              <Icon name="x" class="w-4 h-4" />
            </button>
          </div>
        {/each}
      </div>
    {/each}
  </section>
</div>

<style>
  .ndt {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
  }
  .ndt-intro {
    font-size: 0.75rem;
    color: var(--muted-foreground);
    line-height: 1.45;
    margin: 0;
  }
  .ndt-section {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }
  .ndt-section-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    flex-wrap: wrap;
  }
  .ndt-h {
    margin: 0;
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--muted-foreground);
  }
  .ndt-hint {
    margin: 0 0 0.25rem 0;
    font-size: 0.65rem;
    color: var(--muted-foreground);
  }
  .ndt-checkbox-row {
    display: flex;
    align-items: flex-start;
    gap: 0.5rem;
    font-size: 0.8rem;
    cursor: pointer;
    margin-top: 0.35rem;
  }
  .ndt-checkbox-row input {
    margin-top: 0.15rem;
  }
  .ndt-label {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--foreground);
  }
  .ndt-input,
  .ndt-select {
    padding: 0.45rem 0.6rem;
    font-size: 0.8rem;
    border: 1px solid var(--border);
    border-radius: 0.5rem;
    background: var(--background);
    color: var(--foreground);
    outline: none;
  }
  .ndt-input:focus,
  .ndt-select:focus {
    border-color: var(--primary);
  }
  .ndt-input-grow {
    flex: 1;
    min-width: 0;
  }
  .ndt-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }
  .ndt-row--display {
    flex-wrap: wrap;
  }
  .ndt-search-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    gap: 0.5rem;
    align-items: start;
  }
  @media (max-width: 520px) {
    .ndt-search-grid {
      grid-template-columns: 1fr;
    }
  }
  .ndt-search-item {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    min-width: 0;
  }
  .ndt-select-ven--search {
    flex: 1;
    min-width: 0;
  }
  .ndt-filter-table {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    width: 100%;
  }
  .ndt-filter-grid {
    display: grid;
    grid-template-columns: minmax(7rem, 1.15fr) minmax(7rem, 1fr) minmax(8rem, 1.2fr) auto;
    gap: 0.4rem;
    align-items: center;
  }
  @media (max-width: 900px) {
    .ndt-filter-grid {
      grid-template-columns: 1fr 1fr;
    }
    .ndt-filter-grid.ndt-filter-head {
      display: none;
    }
  }
  @media (max-width: 520px) {
    .ndt-filter-grid {
      grid-template-columns: 1fr;
    }
  }
  .ndt-filter-head {
    font-size: 0.6rem;
    font-weight: 700;
    text-transform: uppercase;
    color: var(--muted-foreground);
  }
  .ndt-select-ven--filter-col {
    min-width: 0;
  }
  .ndt-select--filter-op {
    font-size: 0.75rem;
    min-width: 0;
  }
  .ndt-filter-no-value {
    font-size: 0.75rem;
    color: var(--muted-foreground);
    padding: 0.4rem 0;
  }
  .ndt-select-ven--connector {
    margin-top: 0.35rem;
    max-width: 28rem;
  }
  .ndt-connector-map-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    margin-top: 0.65rem;
  }
  .ndt-connector-map-toolbar-title {
    font-size: 0.65rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--muted-foreground);
  }
  .ndt-connector-map-mode {
    display: inline-flex;
    border-radius: 0.375rem;
    border: 1px solid var(--border);
    overflow: hidden;
    background: var(--muted);
  }
  .ndt-connector-mode-btn {
    padding: 0.25rem 0.6rem;
    font-size: 0.65rem;
    font-weight: 600;
    border: none;
    background: transparent;
    color: var(--muted-foreground);
    cursor: pointer;
  }
  .ndt-connector-mode-btn:hover {
    color: var(--foreground);
  }
  .ndt-connector-mode-btn--active {
    background: var(--background);
    color: var(--foreground);
    box-shadow: 0 0 0 1px var(--border);
  }
  .ndt-connector-map {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    margin-top: 0.5rem;
    max-width: 42rem;
  }
  .ndt-connector-map-head {
    display: grid;
    grid-template-columns: minmax(10rem, 1.1fr) minmax(12rem, 1fr);
    gap: 0.5rem;
    font-size: 0.6rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--muted-foreground);
    padding: 0 0.1rem;
  }
  @media (max-width: 640px) {
    .ndt-connector-map-head {
      display: none;
    }
  }
  .ndt-connector-map-row {
    display: grid;
    grid-template-columns: minmax(10rem, 1.1fr) minmax(12rem, 1fr);
    gap: 0.5rem;
    align-items: start;
  }
  @media (max-width: 640px) {
    .ndt-connector-map-row {
      grid-template-columns: 1fr;
    }
  }
  .ndt-connector-key-cell {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
    min-width: 0;
    padding-top: 0.2rem;
  }
  .ndt-connector-key-code {
    font-size: 0.72rem;
    font-weight: 600;
    color: var(--foreground);
    word-break: break-all;
  }
  .ndt-connector-key-label {
    font-size: 0.72rem;
    color: var(--foreground);
  }
  .ndt-connector-key-hint {
    font-size: 0.62rem;
    line-height: 1.35;
    color: var(--muted-foreground);
  }
  .ndt-select-ven--connector-col {
    min-width: 0;
  }
  .ndt-connector-json-wrap {
    margin-top: 0.35rem;
    max-width: 42rem;
  }
  .ndt-connector-json-caption {
    font-size: 0.62rem;
    color: var(--muted-foreground);
    margin-bottom: 0.35rem;
  }
  .ndt-connector-json {
    width: 100%;
    margin-top: 0;
    font-family: ui-monospace, monospace;
    font-size: 0.7rem;
    padding: 0.5rem;
    border-radius: 0.375rem;
    border: 1px solid var(--border);
    background: var(--background);
  }
  .ndt-hint--sm {
    font-size: 0.62rem;
    margin-top: 0.35rem;
  }
  .ndt-select-ven {
    flex: 1;
    min-width: 0;
  }
  .ndt-select-ven--format {
    flex: 0 1 11rem;
    min-width: 9rem;
    max-width: 14rem;
  }
  .ndt-select-ven--field {
    min-width: 0;
  }
  .ndt-select-ven--cond {
    flex: 1 1 10rem;
    min-width: 8rem;
    max-width: 20rem;
  }
  .ndt-select-ven--type {
    min-width: 0;
  }
  .ndt-field-grid {
    display: grid;
    grid-template-columns: 1fr 1fr minmax(5rem, 0.5fr) 1fr auto;
    gap: 0.4rem;
    align-items: center;
  }
  @media (max-width: 720px) {
    .ndt-field-grid {
      grid-template-columns: 1fr;
    }
    .ndt-field-head {
      display: none;
    }
  }
  .ndt-field-head {
    font-size: 0.6rem;
    font-weight: 700;
    text-transform: uppercase;
    color: var(--muted-foreground);
  }
  .ndt-btn-add {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.3rem 0.5rem;
    font-size: 0.65rem;
    font-weight: 600;
    border: 1px solid var(--border);
    border-radius: 0.375rem;
    background: var(--muted);
    color: var(--foreground);
    cursor: pointer;
  }
  .ndt-btn-add:hover {
    border-color: var(--ring);
  }
  .ndt-btn-add--sm {
    padding: 0.2rem 0.4rem;
    font-size: 0.6rem;
  }
  .ndt-icon-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 2rem;
    height: 2rem;
    border: none;
    border-radius: 0.375rem;
    background: transparent;
    color: var(--muted-foreground);
    cursor: pointer;
  }
  .ndt-icon-btn:hover:not(:disabled) {
    background: var(--muted);
    color: var(--foreground);
  }
  .ndt-icon-btn:disabled {
    opacity: 0.35;
    cursor: not-allowed;
  }
  .ndt-empty {
    margin: 0;
    font-size: 0.7rem;
    color: var(--muted-foreground);
    font-style: italic;
  }
  .ndt-empty--sm {
    font-size: 0.65rem;
  }
  .ndt-approval-card {
    border: 1px solid var(--border);
    border-radius: 0.75rem;
    padding: 0.75rem;
    background: var(--muted);
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }
  .ndt-approval-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }
  .ndt-approval-title {
    font-size: 0.75rem;
    font-weight: 700;
  }
  .ndt-when-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-top: 0.25rem;
  }
  .ndt-when-title {
    font-size: 0.65rem;
    font-weight: 600;
    color: var(--muted-foreground);
  }
  .ndt-cond {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    align-items: center;
  }
  .ndt-cond .ndt-select {
    min-width: 8rem;
  }
</style>
