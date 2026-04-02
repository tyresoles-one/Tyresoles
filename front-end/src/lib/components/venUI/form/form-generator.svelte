<script lang="ts">
    import * as Field from '$lib/components/ui/field';
    import { Input } from '$lib/components/ui/input';
    import { Textarea } from '$lib/components/ui/textarea';
    import { Checkbox } from '$lib/components/ui/checkbox';
    import * as Select from '$lib/components/ui/select';
    import { Select as VenSelect } from '$lib/components/venUI/select';
    import { Icon } from '$lib/components/venUI/icon';
    import { DatePicker } from '$lib/components/venUI/date-picker';
    import { Button } from '$lib/components/ui/button';
    import FormGenerator from './form-generator.svelte';
    import type { FormNode, FormSchema } from './types';
    import type { VenForm } from './form.svelte.ts';
	import { cn } from '$lib/utils';
    import { focusManager } from './focus-manager';

    type Props = {
        schema: FormSchema;
        form: VenForm<any>;
        root?: boolean; // Prop to indicate root level for focus management
        autoFocus?: boolean; // Enable auto-focus on first field
    };

    let { schema, form, root = false, autoFocus = false }: Props = $props();

    // Local state for password visibility toggles
    let showPassword = $state<Record<string, boolean>>({});

    function togglePassword(name: string) {
        showPassword[name] = !showPassword[name];
    }

    // Helper to dispatch next-focus event from non-input elements
    function triggerNextFocus(e: any) {
        const trigger = document.activeElement as HTMLElement;
        if (trigger) {
            trigger.dispatchEvent(new CustomEvent('ven-form:next-focus', { bubbles: true }));
        }
    }
</script>

{#snippet content()}
  {#each schema as node}
    <!-- LAYOUT NODES -->
    {#if node.type === "group"}
      {#if node.children.some((c) => c.type === "grid")}
        <div class={node.class || "contents"}>
          <FormGenerator schema={node.children} {form} root={false} />
        </div>
      {:else}
        <Field.Group class={node.class}>
          <FormGenerator schema={node.children} {form} root={false} />
        </Field.Group>
      {/if}
    {:else if node.type === "set"}
      <Field.Set class={node.class}>
        <FormGenerator schema={node.children} {form} root={false} />
      </Field.Set>
    {:else if node.type === "grid"}
      <!-- Responsive Grid logic -->
      <div
        class={cn(node.class || "grid w-full items-start gap-4", !node.mobileCols && "grid-cols-1")}
        style="--desktop-cols: {node.cols || 1}; --mobile-cols: {node.mobileCols || 1}"
        data-responsive-grid
      >
        <FormGenerator schema={node.children} {form} root={false} />
      </div>
    {:else if node.type === "legend"}
      <Field.Legend>{node.text}</Field.Legend>
    {:else if node.type === "description"}
      <Field.Description>{node.text}</Field.Description>
    {:else if node.type === "separator"}
      <Field.Separator />

      <!-- INPUT FIELDS -->
    {:else if node.type === "field"}
      {@const error = form.errors[node.name]}
      {@const isPassword = node.inputType === "password"}
      {@const actualType = isPassword
        ? showPassword[node.name]
          ? "text"
          : "password"
        : node.inputType || "text"}
      {@const hasValue = !!form.values[node.name]}
      {@const showClear = node.clearable && hasValue && !node.disabled}

      <Field.Field
        class={node.class}
        style={node.colSpan ? `--desktop-span: ${node.colSpan}` : undefined}
        data-col-span={!!node.colSpan}
        data-invalid={!!error}
        aria-invalid={!!error}
        orientation={node.orientation}
      >
        {#if node.label}
          <Field.Label 
            class={cn(
              (node.orientation === 'horizontal' || node.orientation === 'responsive') && "flex-none sm:w-28 md:w-32"
            )}
          >
            {node.label}
            {#if (node as any).required}
              <span class="text-destructive ml-1">*</span>
            {/if}
          </Field.Label>
        {/if}

        <Field.Content 
          class="min-w-0" 
          style={node.fieldWidth ? `max-width: ${node.fieldWidth}` : ''}
        >
          {#if node.inputType === "textarea"}
            <div class="relative">
              <Textarea
                bind:value={form.values[node.name]}
                placeholder={node.placeholder}
                disabled={node.disabled}
                aria-invalid={!!error}
                onblur={() => {
                  form.setTouched(node.name);
                  node.onBlur?.(form.values[node.name], node.name, form);
                }}
              />
            </div>
          {:else if node.inputType === "checkbox"}
            <div class="flex items-center space-x-2">
              <Checkbox
                bind:checked={form.values[node.name]}
                id={node.name}
                aria-invalid={!!error}
                onCheckedChange={() => {
                  form.setTouched(node.name);
                  node.onBlur?.(form.values[node.name], node.name, form);
                }}
              />
            </div>
          {:else if node.inputType === "select"}
            <VenSelect
              options={node.options || []}
              bind:value={form.values[node.name]}
              placeholder={node.placeholder || "Select..."}
              labelKey="label"
              valueKey="value"
              disabled={node.disabled}
              aria-invalid={!!error}
              onSelect={(item: any) => {
                form.setTouched(node.name);
                node.onBlur?.(form.values[node.name], node.name, form);
                setTimeout(() => triggerNextFocus(null), 50);
              }}
            />
          {:else if node.inputType === "date-picker"}
            <DatePicker
              value={form.values[node.name]}
              placeholder={node.placeholder}
              disabled={node.disabled}
              showTime={node.showTime}
              picker={node.picker}
              mode={node.mode === 'multiple' ? 'single' : node.mode}
              valueFormat={node.valueFormat}
              displayFormat={node.displayFormat}
              presets={node.presets}
              presetKeys={node.presetKeys}
              workdate={node.workdate}
              aria-invalid={!!error}
              onValueChange={(val: any) => {
                form.setValue(node.name, val);
                form.setTouched(node.name);
                node.onBlur?.(val, node.name, form);
              }}
            />
          {:else}
            <!-- Default Input: one focus ring on the wrapper; slot controls are not separate tab stops -->
            <div
              class={cn(
                "relative rounded-md ring-offset-background transition-[box-shadow,border-color]",
                error
                  ? "focus-within:ring-[3px] focus-within:ring-destructive/20 dark:focus-within:ring-destructive/40 focus-within:border-destructive focus-within:ring-offset-2"
                  : "focus-within:ring-[3px] focus-within:ring-ring/50 focus-within:border-ring focus-within:ring-offset-2",
              )}
            >
              {#if node.leftIcon}
                <div
                  class="absolute inset-y-0 start-0 flex items-center justify-center ps-3 text-muted-foreground pointer-events-none"
                >
                  <Icon name={node.leftIcon} class="size-4" />
                </div>
              {/if}

              <Input
                type={actualType}
                bind:value={form.values[node.name]}
                placeholder={node.placeholder}
                disabled={node.disabled}
                aria-invalid={!!error}
                onblur={() => {
                  form.setTouched(node.name);
                  node.onBlur?.(form.values[node.name], node.name, form);
                }}
                class={cn(
                  node.leftIcon && "ps-10",
                  (node.rightIcon ||
                    isPassword ||
                    node.rightAction ||
                    showClear) &&
                    "pe-10",
                  "focus-visible:ring-0 focus-visible:ring-offset-0",
                )}
              />

              {#if isPassword}
                <button
                  type="button"
                  tabindex="-1"
                  data-ven-form-slot
                  onclick={() => togglePassword(node.name)}
                  class="absolute inset-y-0 end-0 flex h-full w-9 items-center justify-center text-muted-foreground/80 hover:text-foreground rounded-e-md outline-none ring-0 focus:outline-none focus-visible:outline-none focus-visible:ring-0"
                  aria-label={showPassword[node.name]
                    ? "Hide password"
                    : "Show password"}
                >
                  <Icon
                    name={showPassword[node.name] ? "eye-off" : "eye"}
                    class="size-4"
                  />
                </button>
              {:else if showClear}
                <button
                  type="button"
                  tabindex="-1"
                  data-ven-form-slot
                  onclick={() => form.setValue(node.name, "")}
                  class="absolute inset-y-0 end-0 flex h-full w-9 items-center justify-center text-muted-foreground/80 hover:text-foreground rounded-e-md outline-none ring-0 focus:outline-none focus-visible:outline-none focus-visible:ring-0"
                  aria-label="Clear input"
                >
                  <Icon name="x" class="size-4" />
                </button>
              {:else if node.rightAction}
                <button
                  type="button"
                  tabindex="-1"
                  data-ven-form-slot
                  onclick={() => {
                    /* TODO */
                  }}
                  class="absolute inset-y-0 end-0 flex h-full w-9 items-center justify-center text-muted-foreground/80 hover:text-foreground rounded-e-md outline-none ring-0 focus:outline-none focus-visible:outline-none focus-visible:ring-0"
                >
                  <Icon name={node.rightAction.icon} class="size-4" />
                </button>
              {:else if node.rightIcon}
                <div
                  class="absolute inset-y-0 end-0 flex items-center justify-center pe-3 text-muted-foreground pointer-events-none"
                >
                  <Icon name={node.rightIcon} class="size-4" />
                </div>
              {/if}
            </div>
          {/if}
        </Field.Content>

        {#if node.description}
          <Field.Description>{node.description}</Field.Description>
        {/if}

        {#if error}
          <Field.Error>{error}</Field.Error>
        {/if}
      </Field.Field>

      <!-- CUSTOM COMPONENT -->
    {:else if node.type === "custom"}
      <div 
        class={cn("min-w-0 w-full", node.class)}
        style={node.colSpan ? `--desktop-span: ${node.colSpan}` : undefined}
        data-col-span={!!node.colSpan}
      >
        <node.component {...node.props} {form} orientation={node.orientation} />
      </div>
    {/if}
  {/each}
{/snippet}

<!-- If root, apply focus manager action wrapper. recursive calls inside snippet already pass root={false} -->
{#if root}
  <!-- Real box (not display:contents) so focus-manager querySelector + event bubbling stay reliable. -->
  <div use:focusManager={{ autoFocus }} class="w-full min-w-0">
    {@render content()}
  </div>
{:else}
  {@render content()}
{/if}

<style>
    @media (max-width: 767px) {
        div[data-responsive-grid] {
            grid-template-columns: repeat(var(--mobile-cols, 1), minmax(0, 1fr)) !important;
        }
    }
    @media (min-width: 768px) {
        div[data-responsive-grid] {
            grid-template-columns: repeat(var(--desktop-cols), minmax(0, 1fr)) !important;
        }
        :global(div[data-col-span]) {
            grid-column: span var(--desktop-span) !important;
        }
    }
</style>
