<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
    import { Button } from '$lib/components/ui/button';
	import type { ToastOptions } from './types';
    import { toast } from 'svelte-sonner';

    type Props = {
        id?: string | number;
        options: ToastOptions;
    };

	let { id = 'toast', options }: Props = $props();

    function getIconName(type: ToastOptions['type'] = 'default') {
        if (options.icon) return options.icon;
        switch (type) {
            case 'success': return 'circle-check';
            case 'error': return 'octagon-x'; // or circle-x
            case 'warning': return 'triangle-alert';
            case 'info': return 'info';
            default: return 'info';
        }
    }

    function getIconColor(type: ToastOptions['type'] = 'default') {
        switch (type) {
            case 'success': return 'text-emerald-500';
            case 'error': return 'text-destructive';
            case 'warning': return 'text-amber-500';
            case 'info': return 'text-blue-500';
            default: return 'text-foreground';
        }
    }
</script>

<div
  class="border-border bg-background w-[var(--width)] max-w-[calc(100vw-2rem)] rounded-lg border px-4 py-3 shadow-lg"
>
  <div class="flex gap-2">
    <div class="flex grow gap-3">
      {#if options.type !== "default" || options.icon}
        <Icon
          name={getIconName(options.type)}
          class="mt-0.5 shrink-0 {getIconColor(options.type)}"
          size={16}
        />
      {/if}

      <div
        class={options.action || options.cancel
          ? "flex grow flex-wrap justify-between gap-4"
          : "flex grow flex-col gap-1"}
      >
        <div class="flex flex-col gap-1">
          {#if options.title}
            <p class="text-sm font-semibold">{options.title}</p>
          {/if}
          <p class="text-sm text-muted-foreground">{options.description}</p>
        </div>

        {#if options.action || options.cancel}
          <div class="text-sm whitespace-nowrap flex items-center">
            {#if options.action}
              <button
                class="text-sm font-medium hover:underline"
                onclick={() => {
                  options.action?.onClick();
                  toast.dismiss(id);
                }}
              >
                {options.action.label}
              </button>
            {/if}

            {#if options.action && options.cancel}
              <span class="text-muted-foreground mx-2">·</span>
            {/if}

            {#if options.cancel}
              <button
                class="text-sm font-medium hover:underline text-muted-foreground"
                onclick={() => {
                  options.cancel?.onClick?.();
                  toast.dismiss(id);
                }}
              >
                {options.cancel.label}
              </button>
            {/if}
          </div>
        {/if}
      </div>
    </div>

    <button
      class="group -my-1.5 -me-2 size-8 shrink-0 flex items-center justify-center rounded-md hover:bg-muted transition-colors"
      aria-label="Close"
      onclick={() => toast.dismiss(id)}
    >
      <Icon
        name="x"
        size={14}
        class="opacity-40 transition-opacity group-hover:opacity-100"
      />
    </button>
  </div>
</div>
