<script lang="ts">
  import { page } from "$app/stores";
  import { Home, FileText, Users, Menu } from "@lucide/svelte";
  import { authStore } from "$lib/stores";
  const navItems = [
    { name: "Home", path: "/", icon: Home },
    { name: "Reports", path: "/reports", icon: FileText },
    //{ name: 'Users', path: '/users', icon: Users },
    { name: "Menu", path: "/menu", icon: Menu },
  ];

  // Derived states explicitly typed to handle pathname string matching seamlessly
  let currentPath = $derived($page.url.pathname);
</script>

{#if $authStore.token}
  <nav
    style="padding-bottom: env(safe-area-inset-bottom);"
    class="md:hidden fixed bottom-0 left-0 w-full z-50 bg-background/90 backdrop-blur-2xl border-t border-border flex justify-around items-center h-16 shadow-[0_-8px_30px_rgba(0,0,0,0.08)]"
  >
    {#each navItems as item}
      {@const isActive =
        currentPath === item.path ||
        (item.path !== "/" && currentPath.startsWith(item.path))}
      {@const Icon = item.icon}
      <a
        href={item.path}
        class="flex flex-col items-center justify-center w-full h-full space-y-1 touch-manipulation transition-all duration-300 active:scale-90"
        class:text-primary={isActive}
        class:text-muted-foreground={!isActive}
        aria-label={item.name}
      >
        <div class="relative flex flex-col items-center">
          <Icon
            size={22}
            strokeWidth={isActive ? 2.5 : 1.5}
            class="transition-all duration-300 {isActive
              ? 'scale-110 drop-shadow-[0_0_8px_rgba(var(--accent),0.3)]'
              : ''}"
          />
          <!-- Active Indicator Dot (Accent Yellow) -->
          {#if isActive}
            <span
              class="absolute -bottom-3 w-1.5 h-1.5 bg-accent rounded-full animate-in zoom-in-0 fade-in-0 slide-in-from-bottom-2 duration-500 shadow-[0_0_10px_var(--accent)]"
            ></span>
          {/if}
        </div>
        <span
          class="text-[10px] uppercase font-black tracking-tighter mt-1.5 transition-colors duration-300"
          class:text-primary={isActive}>{item.name}</span
        >
      </a>
    {/each}
  </nav>

  <!-- Add a Spacer block to any page layout using this to not cover content -->
  <div
    class="h-16 md:hidden w-full pb-[env(safe-area-inset-bottom)]"
    aria-hidden="true"
  ></div>
{/if}
