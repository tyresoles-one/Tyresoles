<script lang="ts">
	import * as AlertDialog from '$lib/components/ui/alert-dialog';
	import { Icon } from '$lib/components/venUI/icon';
	import { buttonVariants } from '$lib/components/ui/button';
	import { dialogService } from './service.svelte.ts';

    // We only access the state here. Svelte 5 reactivity handles the rest.
    // dialogService.dialogs is a getter for the state.
</script>

{#each dialogService.dialogs as dialog (dialog.id)}
  <AlertDialog.Root
    open={dialog.open}
    onOpenChange={(open) => {
      if (!open) dialogService._dismiss(dialog.id, null);
    }}
  >
    <AlertDialog.Content>
      <div class="flex flex-col gap-4">
        {#if dialog.icon}
          <div class="flex items-start gap-4">
            <div class="rounded-full bg-muted p-2">
              <Icon name={dialog.icon} class="size-6 text-foreground" />
            </div>
            <div class="flex-1">
              <AlertDialog.Header>
                <AlertDialog.Title>{dialog.title}</AlertDialog.Title>
                {#if dialog.description}
                  <AlertDialog.Description
                    >{dialog.description}</AlertDialog.Description
                  >
                {/if}
              </AlertDialog.Header>
            </div>
          </div>
        {:else}
          <AlertDialog.Header>
            <AlertDialog.Title>{dialog.title}</AlertDialog.Title>
            {#if dialog.description}
              <AlertDialog.Description
                >{dialog.description}</AlertDialog.Description
              >
            {/if}
          </AlertDialog.Header>
        {/if}

        {#if dialog.content}
          <!-- Placeholder for custom content injection if we implemented that -->
          <!-- {@render dialog.content()} -->
        {/if}

        <AlertDialog.Footer>
          {#if dialog.actions && dialog.actions.length > 0}
            {#each dialog.actions as action}
              {#if action.variant === "outline"}
                <AlertDialog.Cancel
                  class={action.class || buttonVariants({ variant: "outline" })}
                  onclick={() =>
                    dialogService._dismiss(dialog.id, action.value)}
                >
                  {action.label}
                </AlertDialog.Cancel>
              {:else}
                <AlertDialog.Action
                  class={action.class ||
                    buttonVariants({ variant: action.variant || "default" })}
                  onclick={() =>
                    dialogService._dismiss(dialog.id, action.value)}
                >
                  {action.label}
                </AlertDialog.Action>
              {/if}
            {/each}
          {:else}
            <!-- Default Actions -->
            {#if dialog.cancelLabel}
              <AlertDialog.Cancel
                onclick={() => dialogService._dismiss(dialog.id, false)}
              >
                {dialog.cancelLabel}
              </AlertDialog.Cancel>
            {/if}
            <AlertDialog.Action
              onclick={() => dialogService._dismiss(dialog.id, true)}
            >
              {dialog.confirmLabel || "Continue"}
            </AlertDialog.Action>
          {/if}
        </AlertDialog.Footer>
      </div>
    </AlertDialog.Content>
  </AlertDialog.Root>
{/each}
