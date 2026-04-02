<script lang="ts">
	import {
		Navbar,
		NavbarBrand,
		NavbarNav,
		NavbarItem,
		NavbarMobileMenu,
        NavbarDropdown
	} from '$lib/components/venUI/navbar';
	import { Dropdown } from '$lib/components/venUI/dropdowns';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
    import { Avatar, AvatarImage, AvatarFallback } from '$lib/components/ui/avatar';
	import { page } from '$app/stores';

	let activeLink = $state('/ven-ui-dev/navbar');
    
    // Fake page content for scrolling
    let items = Array.from({ length: 20 }, (_, i) => `Content Section ${i + 1}`);

    const productItems = [
        { type: 'item', label: 'New Arrivals', icon: 'sparkles' },
        { type: 'item', label: 'Best Sellers', icon: 'trophy' },
        { type: 'separator' },
        { type: 'item', label: 'Electronics', icon: 'monitor' },
        { type: 'item', label: 'Clothing', icon: 'shirt' }
    ];

    const userDropdownItems = [
// ... existing userDropdownItems
        { type: 'label', label: 'My Account' },
        { type: 'separator' },
        { type: 'item', label: 'Profile', icon: 'user' },
        { type: 'item', label: 'Settings', icon: 'settings' },
        { type: 'item', label: 'Billing', icon: 'credit-card' },
        { type: 'separator' },
        { type: 'item', label: 'Log out', icon: 'log-out', className: 'text-red-500' }
    ];
</script>

<div class="space-y-6">
  <div>
    <h1 class="text-3xl font-bold mb-2">Navbar Component</h1>
    <p class="text-muted-foreground">
      A responsive, composable navbar with mobile drawer support, sticky
      positioning, and glassmorphism effects.
    </p>
  </div>

  <div
    class="border rounded-xl overflow-hidden shadow-sm h-[600px] flex flex-col relative bg-background"
  >
    <!-- Demo Navbar -->
    <!-- Note: In a real app, this would be in +layout.svelte -->

    <Navbar class="absolute inset-x-0 top-0">
      <!-- Mobile Menu Trigger & Content -->
      <NavbarMobileMenu>
        <NavbarItem
          href="#"
          onclick={() => (activeLink = "#")}
          isActive={activeLink === "#"}
        >
          <Icon name="layout-dashboard" /> Dashboard
        </NavbarItem>
        <NavbarItem
          href="#users"
          onclick={() => (activeLink = "#users")}
          isActive={activeLink === "#users"}
        >
          <Icon name="users" /> Users
        </NavbarItem>
        <NavbarItem
          href="#products"
          onclick={() => (activeLink = "#products")}
          isActive={activeLink === "#products"}
        >
          <Icon name="package" /> Products
        </NavbarItem>
        <NavbarItem
          href="#orders"
          onclick={() => (activeLink = "#orders")}
          isActive={activeLink === "#orders"}
        >
          <Icon name="shopping-cart" /> Orders
        </NavbarItem>
        <NavbarItem
          href="#analytics"
          onclick={() => (activeLink = "#analytics")}
          isActive={activeLink === "#analytics"}
        >
          <Icon name="chart-column" /> Analytics
        </NavbarItem>
      </NavbarMobileMenu>

      <!-- Brand -->
      <NavbarBrand href="/ven-ui-dev/navbar">
        <div
          class="size-8 rounded bg-primary text-primary-foreground flex items-center justify-center"
        >
          <Icon name="layers" class="size-5" />
        </div>
        <span class="hidden sm:inline-block">Acme Inc.</span>
      </NavbarBrand>

      <!-- Desktop Nav -->
      <NavbarNav class="hidden md:flex mx-6">
        <NavbarItem
          href="#"
          onclick={() => (activeLink = "#")}
          isActive={activeLink === "#"}
        >
          Dashboard
        </NavbarItem>
        <NavbarItem
          href="#users"
          onclick={() => (activeLink = "#users")}
          isActive={activeLink === "#users"}
        >
          Users
        </NavbarItem>
        <NavbarDropdown
          label="Products"
          items={productItems as any}
          isActive={activeLink === "#products"}
        />
        <NavbarItem
          href="#settings"
          onclick={() => (activeLink = "#settings")}
          isActive={activeLink === "#settings"}
        >
          Settings
        </NavbarItem>
      </NavbarNav>

      <!-- Right Actions -->
      <div class="ml-auto flex items-center gap-2">
        <Button variant="ghost" size="icon" class="text-muted-foreground">
          <Icon name="search" class="size-5" />
        </Button>
        <Button variant="ghost" size="icon" class="text-muted-foreground">
          <Icon name="bell" class="size-5" />
        </Button>
        <Dropdown
          trigger={{ label: "User" }}
          items={userDropdownItems as any}
          align="end"
        >
          {#snippet children({ props })}
            <Button
              variant="ghost"
              class="relative size-8 rounded-full p-0"
              {...props}
            >
              <Avatar class="size-8">
                <AvatarImage
                  src="https://github.com/shadcn.png"
                  alt="@shadcn"
                />
                <AvatarFallback>SC</AvatarFallback>
              </Avatar>
            </Button>
          {/snippet}
        </Dropdown>
      </div>
    </Navbar>

    <!-- Dummy Content Area -->
    <div class="flex-1 overflow-auto bg-muted/10 w-full">
      <div class="container py-20 px-4 md:px-6 space-y-8">
        <div
          class="p-6 rounded-lg bg-blue-500/10 border border-blue-500/20 text-blue-700 dark:text-blue-300"
        >
          <h3 class="font-semibold flex items-center gap-2">
            <Icon name="info" class="size-4" />
            Component Usage
          </h3>
          <p class="mt-2 text-sm">
            Resize the window to see the mobile menu toggle. The navbar is
            sticky and has a backdrop blur effect when scrolling over content.
          </p>
        </div>

        <div class="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {#each items as item}
            <div
              class="h-40 rounded-xl bg-card border p-6 shadow-sm hover:shadow-md transition-all"
            >
              <h3 class="font-semibold text-lg">{item}</h3>
              <div class="mt-4 space-y-2">
                <div class="h-2 w-3/4 rounded bg-muted"></div>
                <div class="h-2 w-1/2 rounded bg-muted"></div>
              </div>
            </div>
          {/each}
        </div>
      </div>
    </div>
  </div>
</div>
