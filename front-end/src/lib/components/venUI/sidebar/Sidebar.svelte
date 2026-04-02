<script lang="ts">
  import { page } from "$app/stores";
  import { authStore } from "$lib/stores/auth";
  import { cn } from "$lib/utils";
  import { Icon } from "$lib/components/venUI/icon";
  import { fly } from "svelte/transition";

  // Extract unique permissions to avoid duplicates if any
  // Assumes permission name or roleId map to routes or we just list them.
  // The requirement says "sidebar populated with permissions".
  // Note: Permission structure from store might not have 'route'.
  // We might need to map permission names to routes, OR assume 'Action' or 'Name' implies a route.
  // For now, I'll assume we list them as buttons or links. The user said "use existing components".
  // If no route is present in permission, we might just disable them or link to #.
  // But typically navigation permissions map to pages.
  // Let's assume the permission `Name` is the label, and we map it to a route if possible, or just render it.

  // Actually, standard NAV permissions might not have routes.
  // But usually "Users" -> /users.
  // I will try to kebab-case the name to guess route.

  // Derive flat menu list from authStore.menus
  // AuthStore.menus is Menu[] -> subMenus: SubMenu[] -> items: MenuItem[]
  let menuItems = $derived.by(() => {
    const items: Array<{ name: string; icon: string; route: string }> = [];
    
    // Add Dashboard and Sales Documents
    items.push({ name: "Dashboard", icon: "layout-dashboard", route: "/" });
    items.push({ name: "Sales Docs", icon: "file-stack", route: "/salesdocs" });

    if ($authStore.menus) {
      for (const menu of $authStore.menus) {
        for (const sub of menu.subMenus) {
          for (const item of sub.items) {
             items.push({
                name: item.label,
                icon: item.icon || "box",
                route: item.action.startsWith("/") ? item.action : `/${item.action}`
             });
          }
        }
      }
    }

    // Ensure Sessions is visible to admins if they have permissions
    // or if the backend includes it in the menus. 
    // For now, if someone is an admin (based on routePermissions or just knowing they want it), 
    // we could add it. But ideally backend sends it.
    
    return items;
  });

  function isActive(route: string) {
    if (route === "/" && $page.url.pathname === "/") return true;
    if (route !== "/" && $page.url.pathname.startsWith(route)) return true;
    return false;
  }

  import { Sheet, SheetContent, SheetTrigger } from "$lib/components/ui/sheet";
  import { Button } from "$lib/components/ui/button";

  let { class: className = "", mobileOpen = $bindable(false) } = $props();

  let isCollapsed = $state(false);

  function toggleCollapse() {
    isCollapsed = !isCollapsed;
  }
</script>

<!-- Mobile Drawer -->
<Sheet bind:open={mobileOpen}>
  <SheetContent
    side="left"
    class="p-0 w-72 bg-background/80 backdrop-blur-xl border-r-border/40 block md:hidden"
  >
    <div class="p-6 border-b border-border/40">
      <span
        class="bg-gradient-to-r from-primary to-purple-600 bg-clip-text text-transparent text-xl font-bold tracking-tight"
      >
        Tyresoles
      </span>
    </div>
    <nav class="space-y-1.5 p-4">
      {#each menuItems as item}
        {@const active = isActive(item.route)}
        <a
          href={item.route}
          onclick={() => (mobileOpen = false)}
          class={cn(
            "flex items-center gap-3 px-3 py-3 rounded-xl text-sm font-medium transition-all duration-200 relative overflow-hidden",
            active
              ? "bg-primary/10 text-primary shadow-sm ring-1 ring-primary/20"
              : "text-muted-foreground hover:text-foreground hover:bg-muted/50",
          )}
        >
          <div
            class={cn(
              "p-1.5 rounded-lg transition-colors",
              active ? "bg-background/50" : "bg-muted/30",
            )}
          >
            <Icon name={item.icon || "box"} class="size-4" />
          </div>
          <span>{item.name || "Unknown"}</span>
        </a>
      {/each}
    </nav>
  </SheetContent>
</Sheet>

<!-- Desktop Sidebar -->
<aside
  class={cn(
    "shrink-0 hidden md:flex flex-col border-r bg-background/50 backdrop-blur-sm h-[calc(100vh-4rem)] sticky top-16 overflow-y-auto transition-[width] duration-300 ease-in-out z-30",
    isCollapsed ? "w-20" : "w-64",
    className,
  )}
>
  <div class="flex-1 py-6 px-3 space-y-1.5">
    {#each menuItems as item, i}
      {@const active = isActive(item.route)}

      <a
        href={item.route}
        title={isCollapsed ? item.name : undefined}
        class={cn(
          "flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200 group relative overflow-hidden",
          active
            ? "bg-primary/10 text-primary shadow-sm ring-1 ring-primary/20"
            : "text-muted-foreground hover:text-foreground hover:bg-muted/50",
          isCollapsed && "justify-center px-0",
        )}
      >
        {#if active}
          <div
            class={cn(
              "absolute inset-0 bg-gradient-to-r from-primary/10 to-transparent opacity-50",
              isCollapsed && "hidden",
            )}
          ></div>
          <div
            class={cn(
              "absolute left-0 top-1/2 -translate-y-1/2 w-1 h-8 bg-primary rounded-full shadow-[0_0_8px_rgba(var(--primary),0.6)]",
              isCollapsed && "left-1",
            )}
          ></div>
        {/if}

        <div
          class={cn(
            "relative z-10 p-1.5 rounded-lg transition-colors shrink-0",
            active
              ? "bg-background/50"
              : "bg-muted/30 group-hover:bg-background",
          )}
        >
          <Icon
            name={item.icon || "box"}
            class={cn(
              "size-4",
              active
                ? "text-primary"
                : "text-muted-foreground group-hover:text-primary",
            )}
          />
        </div>

        <span
          class={cn(
            "relative z-10 truncate transition-all duration-300 origin-left",
            isCollapsed ? "w-0 opacity-0 hidden" : "w-auto opacity-100",
          )}
        >
          {item.name || "Unknown"}
        </span>

        {#if active && !isCollapsed}
          <Icon
            name="chevron-right"
            class="absolute right-2 size-3 text-primary/50 animate-in fade-in slide-in-from-left-2"
          />
        {/if}
      </a>
    {/each}
  </div>

  <!-- Toggle Button -->
  <div class="p-3 border-t border-border/40 mt-auto">
    <Button
      variant="ghost"
      size="sm"
      class={cn("w-full gap-2", isCollapsed && "justify-center px-0")}
      onclick={toggleCollapse}
    >
      <Icon
        name={isCollapsed ? "panel-left-open" : "panel-left-close"}
        class="size-4"
      />
      <span
        class={cn(
          "transition-all duration-300",
          isCollapsed ? "w-0 opacity-0 hidden" : "w-auto opacity-100",
        )}>Collapse</span
      >
    </Button>
  </div>
</aside>
