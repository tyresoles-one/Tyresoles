<script lang="ts">
  import { authStore } from "$lib/stores/auth";
  import { buildMenusFromUser } from "$lib/components/venUI/menubar";
  import { Icon } from "$lib/components/venUI/icon";
  import { goto } from "$app/navigation";
  import { fly, fade } from "svelte/transition";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Button } from "$lib/components/ui/button";
  const navMenus = $derived(
    buildMenusFromUser($authStore.menus, $authStore.user?.userType),
  );
</script>


<PageHeading
  backHref="/"
  backLabel="Back to home"
  icon="menu"
  title="Menu"
  description="Manage your profile and photo"
>
</PageHeading>
<!-- Responsive page only visible on mobile (hidden on desktop via route or responsive layout in Main)
     Wait, it's better to just build the page. If a user visits on desktop, they'll see the page too, but we will hide the bottom nav button on desktop. -->

<div
  class="min-h-screen bg-background text-foreground pb-24 pt-2 px-4 max-w-5xl mx-auto"
  in:fade={{ duration: 200 }}
>
  <div class="space-y-4">
    {#each navMenus as segment, i (segment.id)}
      <section in:fly={{ y: 20, delay: i * 50, duration: 400 }}>
        <!-- Section Header -->
        <div class="flex items-center gap-3 mb-5">
          {#if segment.icon}
            <div class="p-2 rounded-xl bg-primary/10 text-primary">
              <Icon name={segment.icon} class="w-5 h-5" />
            </div>
          {/if}
          <h2 class="text-xl font-bold tracking-tight">{segment.label}</h2>
        </div>

        <!-- Section Content -->
        {#if segment.children?.length}
          <div class="grid grid-cols-3 gap-x-3 gap-y-4">
            {#each segment.children as child}
              {#if child.href}
                <!-- Direct Link Item -->
                <button
                  onclick={() => goto(child.href as string)}
                  class="flex flex-col items-center justify-start py-4 px-2 bg-card border border-border/50 rounded-3xl shadow-sm shadow-black/5 hover:shadow-md hover:border-primary/20 active:scale-[0.97] transition-all duration-300 gap-3 group relative overflow-hidden"
                >
                  <div
                    class="absolute inset-0 bg-gradient-to-br from-primary/0 to-primary/5 opacity-0 group-hover:opacity-100 transition-opacity"
                  ></div>
                  <div
                    class="p-3.5 rounded-2xl bg-muted text-muted-foreground group-hover:bg-primary group-hover:text-primary-foreground group-hover:shadow-[0_0_15px_rgba(var(--primary),0.3)] transition-all duration-300 z-10"
                  >
                    {#if child.icon}
                      <Icon name={child.icon} class="w-6 h-6" />
                    {:else}
                      <Icon name="layout-template" class="w-6 h-6" />
                    {/if}
                  </div>
                  <span
                    class="text-xs font-semibold text-center leading-tight z-10 px-1 text-foreground/80 group-hover:text-primary transition-colors"
                  >
                    {child.label}
                  </span>
                </button>
              {:else if child.children?.length}
                <!-- Nested items, grouping them nicely -->
                <div class="col-span-3 mt-2 mb-3">
                  <h3 class="text-sm font-semibold text-muted-foreground mb-3 px-1 flex items-center gap-2">
                    {#if child.icon}
                      <Icon name={child.icon} class="w-4 h-4 opacity-70" />
                    {/if}
                    {child.label}
                  </h3>
                  <div class="grid grid-cols-2 gap-3">
                    {#each child.children as subChild}
                      <button
                        onclick={() =>
                          subChild.href ? goto(subChild.href) : null}
                        class="flex items-center gap-3 p-3.5 bg-card border border-border/50 rounded-2xl shadow-sm shadow-black/5 hover:shadow-md hover:border-primary/20 active:scale-95 transition-all group relative overflow-hidden"
                      >
                         <div
                          class="absolute inset-0 bg-gradient-to-r from-primary/0 to-primary/5 opacity-0 group-hover:opacity-100 transition-opacity"
                         ></div>
                        <div
                          class="p-2.5 rounded-xl bg-muted text-muted-foreground group-hover:text-primary group-hover:bg-primary/10 transition-colors z-10"
                        >
                          {#if subChild.icon}
                            <Icon name={subChild.icon} class="w-4 h-4" />
                          {:else}
                            <Icon name="file-text" class="w-4 h-4" />
                          {/if}
                        </div>
                        <span
                          class="text-xs font-semibold text-left leading-tight z-10 text-foreground/80 group-hover:text-primary transition-colors"
                          >{subChild.label}</span
                        >
                      </button>
                    {/each}
                  </div>
                </div>
              {/if}
            {/each}
          </div>
        {:else if segment.href}
          <!-- Top-level item with href -->
          <button
            onclick={() => goto(segment.href as string)}
            class="flex items-center p-4 bg-card border border-border/50 rounded-2xl shadow-sm active:scale-95 transition-all text-left group"
          >
            <div
              class="p-3 rounded-full bg-muted text-muted-foreground mr-4 group-hover:bg-primary group-hover:text-primary-foreground transition-colors"
            >
              <Icon name={segment.icon || "layout"} class="w-5 h-5" />
            </div>
            <span class="font-semibold">{segment.label}</span>
            <Icon
              name="chevron-right"
              class="w-4 h-4 ml-auto text-muted-foreground opacity-50 group-hover:opacity-100 group-hover:text-primary group-hover:translate-x-1 transition-all"
            />
          </button>
        {/if}
      </section>
    {/each}
  </div>
</div>
