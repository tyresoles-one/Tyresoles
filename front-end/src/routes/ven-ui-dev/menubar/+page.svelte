<script lang="ts">
  import { ResponsiveMenubar } from "$lib/components/venUI/menubar";
  import type { MenuItem } from "$lib/components/venUI/menubar/types";
  import { toast } from "$lib/components/venUI/toast";

  let menus: MenuItem[] = [
    {
      id: "file",
      label: "File",
      children: [
        {
          id: "new",
          label: "New Tab",
          icon: "plus",
          shortcut: "⌘T",
          onClick: () => toast.info("New Tab created"),
        },
        {
          id: "new-window",
          label: "New Window",
          icon: "app-window",
          shortcut: "⌘N",
          onClick: () => toast.info("New Window created"),
        },
        {
          id: "incognito",
          label: "New Incognito Window",
          disabled: true,
        },
        { type: "separator", id: "sep1", label: "sep1" },
        {
          id: "share",
          label: "Share",
          icon: "share-2",
          children: [
            { id: "email", label: "Email", icon: "mail" },
            { id: "message", label: "Message", icon: "message-square" },
            { id: "twitter", label: "Twitter", icon: "twitter" },
          ],
        },
        { type: "separator", id: "sep2", label: "sep2" },
        {
          id: "print",
          label: "Print",
          icon: "printer",
          shortcut: "⌘P",
          onClick: () => window.print(),
        },
      ],
    },
    {
      id: "edit",
      label: "Edit",
      children: [
        {
          id: "undo",
          label: "Undo",
          icon: "undo",
          shortcut: "⌘Z",
          onClick: () => toast.success("Undo action"),
        },
        {
          id: "redo",
          label: "Redo",
          icon: "redo",
          shortcut: "⇧⌘Z",
          onClick: () => toast.success("Redo action"),
        },
        { type: "separator", id: "sep3", label: "sep3" },
        {
          id: "cut",
          label: "Cut",
          shortcut: "⌘X",
        },
        {
          id: "copy",
          label: "Copy",
          shortcut: "⌘C",
        },
        {
          id: "paste",
          label: "Paste",
          shortcut: "⌘V",
        },
      ],
    },
    {
      id: "view",
      label: "View",
      children: [
        {
          id: "refresh",
          label: "Reload",
          icon: "refresh-ccw",
          shortcut: "⌘R",
        },
        {
          id: "force-reload",
          label: "Force Reload",
          icon: "refresh-cw",
          shortcut: "⇧⌘R",
        },
        { type: "separator", id: "sep4", label: "sep4" },
        {
          id: "show-sidebar",
          label: "Show Sidebar",
          type: "checkbox",
          checked: true,
          onClick: () => toast.info("Toggled Sidebar"),
        },
        {
          id: "show-statusbar",
          label: "Show Status Bar",
          type: "checkbox",
          checked: false,
        },
        {
          id: "theme",
          label: "Theme",
          children: [
            { id: "system", type: "radio", label: "System", checked: true },
            { id: "light", type: "radio", label: "Light" },
            { id: "dark", type: "radio", label: "Dark" },
          ],
        },
      ],
    },
    {
      id: "profiles",
      label: "Profiles",
      children: [
        {
          id: "p1",
          type: "radio",
          label: "Andy",
          checked: true,
        },
        {
          id: "p2",
          type: "radio",
          label: "Benoit",
        },
        {
          id: "p3",
          type: "radio",
          label: "Luis",
        },
        { type: "separator", id: "sep5", label: "sep5" },
        {
          id: "edit-profiles",
          label: "Edit...",
          icon: "pencil",
        },
      ],
    },
  ];

  let customMenus: MenuItem[] = [
    {
      id: "nav",
      label: "Navigation",
      children: [
        { id: "home", label: "Home", href: "/", icon: "home" },
        { id: "users", label: "Users", href: "/users", icon: "users" },
        {
          id: "settings",
          label: "Settings",
          href: "/settings",
          icon: "settings",
        },
      ],
    },
  ];
</script>

<div class="container mx-auto py-10 space-y-10">
  <div class="space-y-4">
    <h1 class="text-3xl font-bold tracking-tight">Responsive Menubar</h1>
    <p class="text-muted-foreground text-lg">
      A responsive, JSON-driven menubar with icons, shortcuts, nested menus,
      selection states, and a "More" overflow when space is limited.
    </p>
  </div>

  <div class="grid gap-8">
    <!-- Example 1: Full Application Menu -->
    <div class="space-y-4">
      <h2 class="text-xl font-semibold">Application Menu</h2>
      <div
        class="p-10 border rounded-xl bg-muted/20 flex justify-center items-start h-[300px]"
      >
        <ResponsiveMenubar {menus} />
      </div>
    </div>

    <!-- Example 2: Navigation Menu -->
    <div class="space-y-4">
      <h2 class="text-xl font-semibold">Navigation Style</h2>
      <div
        class="p-10 border rounded-xl bg-muted/20 flex justify-center items-start"
      >
        <ResponsiveMenubar
          menus={customMenus}
          class="bg-primary/5 text-primary-foreground border-primary/20"
        />
      </div>
    </div>
  </div>
</div>
