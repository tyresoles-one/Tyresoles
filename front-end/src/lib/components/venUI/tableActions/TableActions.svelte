<script lang="ts">
	/**
	 * Table row actions dropdown component
	 * 
	 * @example
	 * ```svelte
	 * <TableActions
	 *   title="John Doe"
	 *   actions={[
	 *     { label: 'View Details', icon: 'eye', onClick: () => goto(`/users/${user.id}`) },
	 *     { label: 'Edit', icon: 'edit', onClick: () => editUser(user) },
	 *     { label: 'Delete', icon: 'trash', onClick: () => deleteUser(user), variant: 'destructive' }
	 *   ]}
	 * />
	 * ```
	 */
	import { Dropdown } from '$lib/components/venUI/dropdowns';
	import type { DropdownItem } from '$lib/components/venUI/dropdowns/types';

	type Action = {
		label: string;
		icon: string;
		onClick: () => void;
		variant?: 'default' | 'destructive';
	};

	type Props = {
		/** Optional title to show in dropdown header */
		title?: string;
		/** Array of action items */
		actions: Action[];
		/** Alignment of dropdown */
		align?: 'start' | 'center' | 'end';
	};

	let { title, actions, align = 'end' }: Props = $props();

	const dropdownItems = $derived<DropdownItem[]>([
		...(title ? [{ type: 'label' as const, label: title }] : []),
		...(title ? [{ type: 'separator' as const }] : []),
		...actions.map(
			(a): DropdownItem => ({
				type: 'item' as const,
				label: a.label,
				icon: a.icon,
				onClick: a.onClick
			})
		)
	]);
</script>

<Dropdown trigger={{ label: 'Actions', icon: 'ellipsis', iconOnly: true }} {align} items={dropdownItems} />
