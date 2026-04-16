<script lang="ts">
	import { cn } from "$lib/utils";
	import type { Component } from "svelte";

	type Props = {
		name: string;
		class?: string;
		[key: string]: any;
	};

	let { name, class: className, ...restProps }: Props = $props();

	let IconComponent: Component | null = $state(null);

	function toKebabCase(str: string) {
		return str.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
	}

    // Use import.meta.glob to create a map of all available icons in node_modules/dist/icons
    // This makes sure Vite finds them, but they are lazy loaded.
    // Note: We access .svelte files directly to avoid potential issues with package exports map in dynamic globs
	const icons = import.meta.glob("/node_modules/@lucide/svelte/dist/icons/*.svelte");

	// Alias map for icons that might have different names in this version or common alternatives
	const ALIASES: Record<string, string> = {
		// Lucide uses `calendar` / `calendar-days`; legacy UI used "Calendar1"
		calendar1: "calendar",
		browser: "globe",
		'filter': 'funnel',
		'loader-2': 'loader-circle',
		'spinner': 'loader-circle',
		'home': 'house',
		'settings2': 'settings-2',
		'alert-circle': 'circle-alert',
		'help-circle': 'circle-help',
		'info-circle': 'circle-info',
		'bar-chart': 'chart-column',
		'bar-chart-2': 'chart-column',
		'bar-chart-3': 'chart-column',
		'bar-chart-4': 'chart-column',
		'check-circle-2': 'circle-check'
	};

	$effect(() => {
		if (!name) return;
		let iconName = toKebabCase(name);
		
		// Check alias
		if (ALIASES[iconName]) {
			iconName = ALIASES[iconName];
		}
        
        const iconKey = `/node_modules/@lucide/svelte/dist/icons/${iconName}.svelte`;
        const loader = icons[iconKey];

		if (loader) {
            loader().then((m: any) => {
                IconComponent = m.default;
            }).catch((err) => {
                console.error(`Failed to load icon: ${name}`, err);
                IconComponent = null;
            });
        } else {
            console.error(`Icon not found: ${name} (key: ${iconKey})`);
            IconComponent = null;
        }
	});
</script>

{#if IconComponent}
  <IconComponent class={cn("size-4", className)} {...restProps} />
{/if}
