<script lang="ts">
	import { onMount } from '$lib';
	import { Grid, Input, DialogPage, toast, updateGoBackPath } from '$lib/components';
	import { loadingStore } from '$lib/managers/stores';
	import { endpoints, apiFetch } from '$lib/managers/network';
	import { fetchItems, insertCasingItems } from '../logic';
	import { TyreFactoriesActive } from '$lib/business';
	import { type CasingItem, type CasingItemWrap } from '$lib/business/models';

	let respCode = $state<string>('');
	let data = $state<CasingItem[]>([]);
	let allItems = $state<CasingItem[]>([]);
	let selectedValues = $state<Set<string>>(new Set());
	let selection = $state<Map<string, object>>(new Map());
	let open = $state(false);
	let selectedRow = $state<CasingItem>({
		code: '',
		minRate: '',
		maxRate: '',
		category: ''
	});

	onMount(() => {
		fetchItems('CASING', '').then((res) => {
			if (res?.success) {
				data = res?.data;
				allItems = res?.data;
			}
		});
		updateGoBackPath('/ecoproc');
	});
	$effect(() => {
		if (!respCode) {
			console.log('respCode', respCode);
			selectedValues = new Set();
			selection.clear();
		}
	});
</script>

<div class="m-1 ml-2 flex gap-2">
	<Input
		label="Select Factory"
		type="list"
		data={TyreFactoriesActive}
		bind:value={respCode}
		dataKey="code"
		labelKey="name"
		hideHeader
		columns={[{ name: 'name', label: 'Name' }]}
		onInput={(respCode) => {
			if (respCode) {
				fetchItems('CASING', 'FromGroupDetail', String(respCode)).then((res) => {
					if (res?.success) {
						if (Array.isArray(res?.data)) {
							selectedValues = new Set(res?.data.map((d) => d.code));
							let newData: CasingItem[] = [];
							allItems.forEach((i) => {
								const item = res?.data.find((d) => d.code === i.code);
								if (item) {
									newData.push({
										code: item.code,
										minRate: item.minRate,
										maxRate: item.maxRate,
										category: item.category
									});
								} else {
									newData.push({
										code: i.code,
										minRate: i.minRate,
										maxRate: i.maxRate,
										category: res?.data[0].category
									});
								}
							});
							data = newData;
						}
					}
				});
			}
		}}
	/>
</div>
<Grid
	{data}
	columns={[
		{ label: 'Tyre Size', name: 'code' },
		{ label: 'Min Rate', name: 'minRate' },
		{ label: 'Max Rate', name: 'maxRate' }
	]}
	actions={[
		{
			icon: 'Save',
			label: 'Save',
			variant: 'outline',
			class: 'border-2',
			onclick: () => {
				if (!respCode) {
					toast.error('Please select a factory');
					return;
				}
				if (selection.size > 0) {
					const items = $state
						.snapshot(data)
						.filter((d) => Array.from(selection.keys()).includes(d.code));

					// Use GraphQL mutation instead of apiFetch
					insertCasingItems(items).then((res) => {
						if (res.success) {
							selection.clear();
							selectedValues = new Set();
						}
					});
				} else {
					toast.error('Please select at least one item');
				}
			}
		},
		{
			icon: 'RefreshCcw',
			label: 'Reload',
			variant: 'outline',
			class: 'border-2',
			onclick: () => {
				fetchItems('CASING', '').then((res) => {
					if (res?.success) data = res?.data;
				});
			}
		}
	]}
	selectionType="multiple"
	enableSelection
	{selectedValues}
	loading={$loadingStore}
	onSelectionChange={(s) => {
		selection = s;
	}}
	onRowClick={(row) => {
		open = true;
		selectedRow = row as CasingItem;
	}}
/>
<DialogPage
	{open}
	onOpenChange={(o) => (open = o)}
	title={`Casing Details for ${selectedRow.code}`}
	actionLabel="Save"
	onAction={() => {
		open = false;
	}}
>
	<div class="flex flex-col gap-2">
		<Input label="Min Rate" bind:value={selectedRow.minRate} />
		<Input label="Max Rate" bind:value={selectedRow.maxRate} />
	</div>
</DialogPage>
