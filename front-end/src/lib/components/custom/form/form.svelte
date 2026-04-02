<script lang="ts">
	import * as Field from "$lib/components/ui/field";
	import { Input } from "$lib/components/ui/input";
	import { Button } from "$lib/components/ui/button";
	import { Separator } from "$lib/components/ui/separator";
	import { Select } from "$lib/components/venUI/select";
	import { Icon } from "$lib/components/venUI/icon";
	import { cn } from "$lib/utils.js";
	import type { FormButtonProps, InputProps } from "../types";

	const inputClass =
		"h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors";

	type Props = {
		fields: InputProps[];
		data?: Record<string, unknown>;
		actions?: FormButtonProps[];
		onSubmit?: (data: Record<string, unknown>) => void;
		loading?: boolean;
		layoutClass?: string;
		/** For `form` attribute on external submit buttons (e.g. PageHeading). */
		formId?: string;
	};

	let {
		fields,
		data = $bindable({} as Record<string, unknown>),
		actions = [],
		onSubmit,
		loading = false,
		layoutClass = "",
		formId = undefined
	}: Props = $props();

	let errors = $state<Record<string, string>>({});

	function fieldError(name: string) {
		return errors[name];
	}

	function validate(): boolean {
		const next: Record<string, string> = {};
		for (const field of fields) {
			const value = data[field.name];
			if (!field.rules?.length) continue;
			for (const rule of field.rules) {
				const r = rule(value, data);
				if (r === true || r === undefined || r === null || r === "") continue;
				next[field.name] = typeof r === "string" ? r : "Invalid";
				break;
			}
		}
		errors = next;
		return Object.keys(next).length === 0;
	}

	function handleFormSubmit(e: Event) {
		e.preventDefault();
		if (!validate()) return;
		onSubmit?.({ ...data });
	}
</script>

<!-- Section headings: match admin dealers [id] (gradient rules + uppercase label) -->
<form
	id={formId}
	class={cn("items-start", layoutClass)}
	onsubmit={handleFormSubmit}
	novalidate
>
	{#each fields as field, fieldIndex (field.name)}
		{#if field.section}
			{#if fieldIndex > 0}
				<Separator class="col-span-full my-4 sm:my-5" />
			{/if}
			<div class="col-span-full flex items-center gap-2 mb-4">
				<div
					class="h-px flex-1 bg-linear-to-r from-primary/20 via-primary/40 to-transparent"
				></div>
				<h3
					class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2"
				>
					{field.section}
				</h3>
				<div
					class="h-px flex-1 bg-linear-to-l from-primary/20 via-primary/40 to-transparent"
				></div>
			</div>
		{/if}
		{@const err = fieldError(field.name)}
		{#if field.type === "list"}
			<Field.Field
				class={cn(
					"min-w-0 flex flex-col gap-1.5",
					field.colSpan === 2 && "col-span-1 sm:col-span-2"
				)}
				data-invalid={!!err}
				aria-invalid={!!err}
			>
				{#if field.label && !field.hideHeader}
					<Field.Label class="text-xs font-medium text-muted-foreground leading-none">
						{field.label}
						{#if field.required}
							<span class="text-destructive ml-1">*</span>
						{/if}
					</Field.Label>
				{/if}
				<Field.Content class="w-full">
					<Select
						options={(field.data ?? []) as Record<string, unknown>[]}
						bind:value={data[field.name]}
						valueKey={(field.dataKey ?? "code") as keyof Record<string, unknown>}
						labelKey={(field.labelKey ?? "name") as keyof Record<string, unknown>}
						columns={(field.columns ?? []).map((c) => ({
							header: c.label ?? c.name,
							accessor: c.name as keyof Record<string, unknown>
						}))}
						disabled={loading || field.readonly}
						multiple={field.selectionType === "multiple"}
						aria-invalid={!!err}
						placeholder={field.label ? `Select ${field.label}` : "Select..."}
						class={cn(inputClass, "w-full")}
						onSelect={(item) => {
							if (!field.onListSelect || item == null) return;
							const vk = (field.dataKey ?? "code") as string;
							const val = (item as Record<string, unknown>)[vk];
							const map = new Map<string, object>();
							for (const row of (field.data ?? []) as Record<string, unknown>[]) {
								const key = String(row[vk] ?? "");
								map.set(key, row as object);
							}
							field.onListSelect(val, map);
						}}
					/>
				</Field.Content>
				{#if err}
					<Field.Error>{err}</Field.Error>
				{/if}
			</Field.Field>
		{:else}
			<Field.Field
				class={cn(
					"min-w-0 flex flex-col gap-1.5",
					field.colSpan === 2 && "col-span-1 sm:col-span-2"
				)}
				data-invalid={!!err}
				aria-invalid={!!err}
			>
				{#if field.label && !field.hideHeader}
					<Field.Label class="text-xs font-medium text-muted-foreground leading-none">
						{field.label}
						{#if field.required}
							<span class="text-destructive ml-1">*</span>
						{/if}
					</Field.Label>
				{/if}
				<Field.Content class="w-full">
					<Input
						type={field.type === "date" ? "date" : "text"}
						bind:value={data[field.name]}
						readonly={field.readonly}
						disabled={loading}
						aria-invalid={!!err}
						class={cn(inputClass, field.readonly && "opacity-90")}
						oninput={(e) => {
							field.oninput?.(e);
							field.onInput?.(e);
						}}
					/>
				</Field.Content>
				{#if err}
					<Field.Error>{err}</Field.Error>
				{/if}
			</Field.Field>
		{/if}
	{/each}

	{#if actions?.length}
		<div
			class="col-span-full flex flex-col gap-3 pt-6 mt-2 border-t border-border/60 sm:flex-row sm:flex-wrap sm:justify-end sm:gap-2"
		>
			{#each actions as action (action.label ?? action.icon)}
				<Button
					type={action.type === "submit" ? "submit" : "button"}
					variant={action.variant ?? "default"}
					class={cn(
						"min-h-11 w-full sm:min-h-9 sm:w-auto inline-flex items-center justify-center gap-2",
						action.class
					)}
					disabled={loading || action.loading}
					aria-busy={action.loading ? true : undefined}
					onclick={(e) => {
						if (action.type !== "submit") {
							e.preventDefault();
							action.onclick?.(e);
						}
					}}
				>
					{#if action.loading}
						<Icon name="loader-2" class="size-4 shrink-0 animate-spin" aria-hidden="true" />
					{:else if action.icon}
						<Icon name={action.icon as any} class="size-4 shrink-0" />
					{/if}
					{action.loading ? (action.loadingLabel ?? action.label ?? "Please wait…") : (action.label ?? "")}
				</Button>
			{/each}
		</div>
	{/if}
</form>
