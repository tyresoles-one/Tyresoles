<script lang="ts">
  import * as Field from "$lib/components/ui/field";
  import { Input } from "$lib/components/ui/input";
  import type { VenForm } from "$lib/components/venUI/form";
  import { Icon } from "$lib/components/venUI/icon";
  import { loginHistoryStore } from "$lib/stores/loginHistory";
  import { onMount } from "svelte";
  import { cn } from "$lib/utils";

  let { 
    form, 
    name, 
    label,
    description = "",
    leftIcon = "user",
    disabled = false,
    clearable = true
  }: { 
    form: VenForm<any>; 
    name: string; 
    label: string;
    description?: string;
    leftIcon?: string;
    disabled?: boolean;
    clearable?: boolean;
  } = $props();

  let sortedHistory = $derived(
    [...$loginHistoryStore].sort((a, b) => {
      // Sort by count (most used), then by lastLogin (most recent)
      if (b.count !== a.count) return b.count - a.count;
      return b.lastLogin - a.lastLogin;
    }).map(e => e.userId)
  );

  onMount(() => {
    // If the form doesn't have a value yet (empty string or undefined), and we have history, auto-fill it
    if (!form.values[name] && sortedHistory.length > 0) {
        form.setValue(name, sortedHistory[0]);
    }
  });
  
  function handleInput(e: Event) {
    const target = e.target as HTMLInputElement;
    let v = target.value;
    
    // Tyresoles custom formatting
    if (v && v.toLowerCase().startsWith("ts:")) {
        v = v.replace(/^ts:/i, "TYRESOLES\\");
        // The bind:value might have already updated form.values, but let's ensure the formatted one is set
        form.setValue(name, v);
    }
  }
</script>

<Field.Field>
  <Field.Label>{label}</Field.Label>
  
  <Field.Content>
    <div class="relative">
      {#if leftIcon}
        <div class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground">
          <Icon name={leftIcon} class="size-4" />
        </div>
      {/if}
      <Input
        id={name}
        {name}
        type="text"
        bind:value={form.values[name]}
        oninput={handleInput}
        {disabled}
        class={cn(leftIcon && "pl-9")}
        placeholder="Enter your username"
        list="login-history-list"
        autocomplete="username"
      />
      
      <datalist id="login-history-list">
        {#each sortedHistory as item}
          <option value={item}></option>
        {/each}
      </datalist>
      
      {#if clearable && form.values[name] && !disabled}
        <button
          type="button"
          tabindex="-1"
          data-ven-form-slot
          class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground focus:outline-none"
          onclick={() => { form.setValue(name, ""); }}
        >
          <Icon name="x" class="size-4" />
        </button>
      {/if}
    </div>
  </Field.Content>

  {#if description}
    <p class="text-[0.8rem] text-muted-foreground mt-1.5">{description}</p>
  {/if}

  {#if form.errors[name]}
    <Field.Error class="font-medium animate-in fade-in slide-in-from-top-1 duration-200 mt-1.5">
      {form.errors[name]}
    </Field.Error>
  {/if}
</Field.Field>
