<script lang="ts">
  import PinField from "$lib/components/venUI/PinField.svelte";
  import type { VenForm } from "$lib/components/venUI/form";

  let { form, name, label }: { form: VenForm<any>; name: string; label: string } = $props();
</script>

<PinField {form} {name} {label} autoSubmit={true} />
