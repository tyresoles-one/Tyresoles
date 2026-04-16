<script lang="ts">
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
  import type { MasterType } from "$lib/components/venUI/master-select/masters-api";

  const fieldName = "v";

  let {
    masterType,
    value = $bindable(),
    label = "",
    placeholder = "Search and select…",
  } = $props<{
    masterType: MasterType;
    value: string;
    label?: string;
    placeholder?: string;
  }>();

  let form = $state({
    values: { [fieldName]: "" } as Record<string, unknown>,
    setTouched(_n: string) {},
    errors: {} as Record<string, string | undefined>,
  });

  $effect(() => {
    form.values[fieldName] = value ?? "";
  });

  $effect(() => {
    const raw = String(form.values[fieldName] ?? "");
    if (raw !== value) value = raw;
  });
</script>

<div class="nemf">
  <MasterSelect
    bind:form
    {fieldName}
    {masterType}
    {label}
    {placeholder}
    singleSelect
  />
</div>

<style>
  .nemf {
    width: 100%;
    min-width: 0;
  }
</style>
