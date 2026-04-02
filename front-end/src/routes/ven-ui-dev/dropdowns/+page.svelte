<script lang="ts">
	import { Dropdown } from '$lib/components/venUI/dropdowns';
	import type { DropdownItem } from '$lib/components/venUI/dropdowns';

	let framework = $state('sveltekit');
	let emailNotifications = $state(true);
	let pushNotifications = $state(false);

    // Using $derived to ensure the menu updates when state changes
	let dropdownItems: DropdownItem[] = $derived([
		{ 
            type: 'group',
            children: [
                { type: 'item', label: 'New', icon: 'plus', shortcut: '⌘N' }
            ]
        },
		{ type: 'separator' },
		{
			type: 'group',
			children: [
				{
					type: 'sub',
					label: 'Framework',
					children: [
						{
							type: 'radio-group',
							value: framework,
							onValueChange: (v) => (framework = v),
							options: [
								{ label: 'SvelteKit', value: 'sveltekit' },
								{ label: 'Next.js', value: 'nextjs', disabled: true },
								{ label: 'Remix', value: 'remix' },
								{ label: 'Astro', value: 'astro' }
							]
						}
					]
				},
				{
					type: 'sub',
					label: 'Notifications',
					children: [
						{
							type: 'checkbox',
							label: 'Email',
							checked: emailNotifications,
							onCheckedChange: (c) => (emailNotifications = c)
						},
						{
							type: 'checkbox',
							label: 'Push',
							checked: pushNotifications,
							onCheckedChange: (c) => (pushNotifications = c)
						}
					]
				}
			]
		},
		{ type: 'separator' },
		{
			type: 'group',
			children: [
				{ type: 'item', label: 'Share', icon: 'share-2' },
				{ type: 'item', label: 'Archive', icon: 'archive-restore' }
			]
		},
		{ type: 'separator' },
		{
			type: 'item',
			label: 'Delete',
			icon: 'trash',
			shortcut: '⌘⌫',
			variant: 'destructive',
		}
	]);
</script>

<div
  class="p-4 md:p-10 flex flex-col gap-4 items-center justify-center min-h-[50vh]"
>
  <div class="mb-6">
    <h1 class="text-2xl font-bold">Dropdown Component</h1>
    <p class="text-muted-foreground">
      Example usage of the data-driven Dropdown component.
    </p>
  </div>

  <Dropdown
    trigger={{
      label: "Rich menu with icons",
      icon: "chevron-down",
      variant: "outline",
    }}
    items={dropdownItems}
  />

  <div class="mt-8 p-4 border rounded-lg bg-muted/50 text-sm">
    <h3 class="font-semibold mb-2">State Monitor</h3>
    <p><strong>Selected Framework:</strong> {framework}</p>
    <p>
      <strong>Email Notifications:</strong>
      {emailNotifications ? "On" : "Off"}
    </p>
    <p>
      <strong>Push Notifications:</strong>
      {pushNotifications ? "On" : "Off"}
    </p>
  </div>
</div>
