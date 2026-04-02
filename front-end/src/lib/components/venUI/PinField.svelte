<script lang="ts">
  import * as InputOTP from "$lib/components/ui/input-otp";
  import * as Field from "$lib/components/ui/field";
  import type { VenForm } from "$lib/components/venUI/form";
  import { REGEXP_ONLY_DIGITS } from "bits-ui";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";

  let { 
    form, 
    name, 
    label, 
    autoSubmit = false,
    autoFocus = false,
    maxLength = 4
  }: { 
    form: VenForm<any>; 
    name: string; 
    label: string;
    autoSubmit?: boolean;
    autoFocus?: boolean;
    maxLength?: number;
  } = $props();

  let value = $state("");
  let showPin = $state(false);

  // Sync value back to form whenever it changes
  $effect(() => {
    form.setValue(name, value);
  });

  // Handle value completion
  $effect(() => {
    if (value.length === maxLength) {
      setTimeout(() => {
        if (value.length === maxLength) { // Double check length
           if (autoSubmit) {
             form.submit();
           } else {
             // Dispatch event to move focus
             const trigger = document.activeElement as HTMLElement;
             if (trigger) {
               trigger.dispatchEvent(new CustomEvent('ven-form:next-focus', { bubbles: true }));
             }
           }
        }
      }, 300);
    }
  });

  function togglePin() {
    showPin = !showPin;
  }
</script>

<Field.Field>
  <div class="flex items-center justify-between transition-opacity duration-200">
    <Field.Label class="mb-0">{label}</Field.Label>
    <button
      type="button"
      onclick={togglePin}
      class="text-[10px] uppercase font-bold tracking-widest text-muted-foreground hover:text-accent flex items-center gap-1.5 transition-colors focus:outline-none"
    >
      <Icon name={showPin ? "eye-off" : "eye"} class="size-3" />
      {showPin ? "Hide" : "Show"} PIN
    </button>
  </div>

  <Field.Content class="flex justify-center py-1">
    <InputOTP.Root
      maxlength={maxLength}
      bind:value
      pattern={REGEXP_ONLY_DIGITS}
      type={showPin ? "text" : "password"}
      class="gap-3"
    >
      {#snippet children({ cells })}
        <InputOTP.Group class="gap-3">
          {#each cells as cell}
            <InputOTP.Slot 
              {cell} 
              type={showPin ? "text" : "password"}
              class="h-14 w-16 text-2xl font-black rounded-xl border-2 transition-all duration-300 shadow-sm ring-offset-background" 
            />
          {/each}
        </InputOTP.Group>
      {/snippet}
    </InputOTP.Root>
  </Field.Content>

  {#if form.errors[name]}
    <Field.Error class="text-center font-medium animate-in fade-in slide-in-from-top-1 duration-200">
      {form.errors[name]}
    </Field.Error>
  {/if}
</Field.Field>
