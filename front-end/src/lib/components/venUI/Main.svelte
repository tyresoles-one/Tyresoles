<script lang="ts">
  import { authStore, clearAuth } from "$lib/stores/auth";
  import { setMode, resetMode } from "mode-watcher";
  import Dropdown from "$lib/components/venUI/dropdowns/dropdown.svelte";
  import { Navbar, NavbarBrand } from "$lib/components/venUI/navbar";
  import {
    ResponsiveMenubar,
    buildMenusFromUser,
  } from "$lib/components/venUI/menubar";
  import * as Avatar from "$lib/components/ui/avatar";
  import { Icon } from "$lib/components/venUI/icon";
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button";
  import { getImages } from "$lib/config/system";
  let { children } = $props();

  const images = $derived(getImages());
  const avatarIndex = $derived(
    Math.max(0, Math.min($authStore.user?.avatar ?? 0, images.length - 1)),
  );

  const navMenus = $derived(
    buildMenusFromUser($authStore.menus, $authStore.user?.userType),
  );
</script>

{#if $authStore.token}
  <Navbar>
    <div class="block">
      <NavbarBrand href="/">
        <div class="flex items-center gap-2">
          <span
            class="bg-gradient-to-r from-primary to-primary bg-clip-text text-transparent text-2xl font-black tracking-tighter"
            >Tyresoles</span
          >
        </div>
      </NavbarBrand>
    </div>

    <div class="hidden md:flex items-center gap-1 min-w-0 flex-1 max-w-2xl">
      <Button
        variant="ghost"
        size="icon"
        class="shrink-0 mr-2 rounded-full hover:bg-accent/10 hover:text-accent transition-all duration-300"
        onclick={() => goto("/")}
        aria-label="Home"
      >
        <Icon
          name="home"
          class="size-5 bg-primary text-primary-foreground rounded-full p-1 shadow-sm"
        />
      </Button>

      {#if navMenus.length > 0}
        <ResponsiveMenubar
          menus={navMenus}
          class="flex-1 min-w-0 p-0"
          moreLabel=""
        />
      {/if}
    </div>

    <div class="ml-auto flex items-center gap-3">
      <Dropdown
        align="end"
        items={[
          { type: "custom", render: userHeader },
          { type: "separator" },
          {
            type: "item",
            label: "Profile",
            icon: "user",
            onClick: () => goto("/profile"),
          },
          {
            type: "item",
            label: "Settings",
            icon: "settings",
            onClick: () => goto("/settings"),
          },
          {
            type: "item",
            label: "Set Password",
            icon: "lock",
            onClick: () => goto("/change-password"),
          },
          {
            type: "sub",
            label: "Theme",
            icon: "sun",
            children: [
              {
                type: "item",
                label: "Light",
                icon: "sun",
                onClick: () => setMode("light"),
              },
              {
                type: "item",
                label: "Dark",
                icon: "moon",
                onClick: () => setMode("dark"),
              },
              {
                type: "item",
                label: "System",
                icon: "laptop",
                onClick: () => resetMode(),
              },
            ],
          },
          { type: "separator" },
          {
            type: "item",
            label: "Log out",
            icon: "log-out",
            onClick: () => {
              clearAuth();
              goto("/login");
            },
          },
        ]}
      >
        {#snippet children({ props })}
          <Avatar.Root
            class="h-8 w-8 cursor-pointer border-2 border-transparent transition-all hover:border-accent hover:scale-105"
            {...props}
          >
            <Avatar.Image
              src={`${images[avatarIndex]?.url ?? ""}?seed=${$authStore.username}`}
              alt={$authStore.username}
            />
            <Avatar.Fallback class="bg-primary text-primary-foreground">
              {$authStore.username?.slice(0, 2).toUpperCase() ?? "U"}
            </Avatar.Fallback>
          </Avatar.Root>
        {/snippet}
      </Dropdown>

      {#snippet userHeader()}
        <div class="flex flex-col space-y-1 px-2 py-2">
          <p class="text-sm font-semibold tracking-tight">
            {$authStore.user?.fullName}
          </p>
          <div class="flex items-center gap-1.5">
            <span class="size-1.5 rounded-full bg-green-500 animate-pulse"
            ></span>
            <p
              class="text-[8px] font-black tracking-widest text-muted-foreground"
            >
              {$authStore.user?.title || "User"}
            </p>
          </div>
        </div>
      {/snippet}
    </div>
  </Navbar>

  <!-- Main Content without Sidebar -->
  <main class="flex-1 w-full min-w-0 container mx-auto py-1 bg-primary/10">
    {@render children()}
  </main>
{:else}
  {@render children()}
{/if}
