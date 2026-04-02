import type { ButtonVariant } from "$lib/components/ui/button";

/** Return `true` if valid; a non-empty string is treated as an error message. */
export type FieldRule = (
	value: unknown,
	data?: Record<string, unknown>
) => string | true | undefined | null;

export type ListColumn = {
	name: string;
	label: string;
	textAlign?: string;
};

/** Legacy ecoproc form field descriptor (used with `Form` in `custom/form/form.svelte`). */
export type InputProps = {
	name: string;
	label?: string;
	type?: string;
	required?: boolean;
	readonly?: boolean;
	hideHeader?: boolean;
	rules?: FieldRule[];
	data?: unknown[];
	dataKey?: string;
	labelKey?: string;
	valueKey?: string;
	columns?: ListColumn[];
	selectionType?: "single" | "multiple";
	oninput?: (e: Event) => void;
	onInput?: (e: Event) => void;
	/** When `type` is `list`, called after a row is chosen; `lookup` maps value key → row (legacy ecoproc). */
	onListSelect?: (value: unknown, lookup: Map<string, object>) => void;
	/** When set on the first field of a group, renders a heading (and separator after the first group). */
	section?: string;
	/** Span full width of the grid from `sm` up (e.g. long address). */
	colSpan?: 1 | 2;
};

/** Toolbar / form action buttons (ecoproc legacy). */
export type FormButtonProps = {
	label?: string;
	icon?: string;
	variant?: ButtonVariant;
	type?: "submit" | "button";
	class?: string;
	onclick?: (e?: MouseEvent) => void;
	menuItems?: { label: string; icon?: string; onClick: () => void }[];
	/** When true, shows a spinner and disables only this button (unless form `loading` is also true). */
	loading?: boolean;
	/** Label while `loading` is true (defaults to the button label). */
	loadingLabel?: string;
	/** Disable this action (e.g. while another toolbar task is running). */
	disabled?: boolean;
};

export type ButtonProps = FormButtonProps;

export type TableColumn = {
	name: string;
	label: string;
	aggregation?: string;
	textAlign?: string;
};

export type MenuItem = {
	label: string;
	href?: string;
	icon?: string;
	onClick?: () => void;
};
