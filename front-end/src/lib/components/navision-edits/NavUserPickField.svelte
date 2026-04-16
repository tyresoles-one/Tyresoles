<script lang="ts">
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";

  const fieldName = "navUsers";

  let {
    multiple = false,
    value = $bindable(),
    label = "",
    placeholder = "Search Nav users…",
    class: className = "",
  } = $props<{
    multiple?: boolean;
    value: string | string[];
    label?: string;
    placeholder?: string;
    class?: string;
  }>();

  let form = $state({
    values: { [fieldName]: "" } as Record<string, unknown>,
    setTouched(_n: string) {},
    errors: {} as Record<string, string | undefined>,
  });

  function valueToStr(v: string | string[]): string {
    if (multiple) return Array.isArray(v) ? v.join(", ") : "";
    return typeof v === "string" ? v : "";
  }

  function parseStr(s: string): string | string[] {
    if (multiple) {
      return s
        .split(",")
        .map((x) => x.trim())
        .filter(Boolean);
    }
    return s.trim();
  }

  $effect(() => {
    form.values[fieldName] = valueToStr(value);
  });

  $effect(() => {
    const raw = String(form.values[fieldName] ?? "");
    const next = parseStr(raw);
    if (multiple) {
      const arr = next as string[];
      const cur = Array.isArray(value) ? value : [];
      if (JSON.stringify(cur) !== JSON.stringify(arr)) {
        value = arr;
      }
    } else {
      const s = next as string;
      if (s !== value) value = s;
    }
  });
</script>

<div class="nup {className}">
  <MasterSelect
    bind:form
    {fieldName}
    masterType="users"
    {label}
    {placeholder}
    singleSelect={!multiple}
  />
</div>

<style>
  .nup {
    width: 100%;
    min-width: 0;
  }
</style>
