import type { ColumnDef, RowData, TableOptions } from '@tanstack/table-core';
import type { SmartPagination } from '$lib/utils/pagination/store.svelte';

export type DataGridColumn<TData extends RowData> = ColumnDef<TData, any>;

export interface DataGridProps<TData extends RowData> {
	/** Title of the grid displayed in the toolbar */
	title?: string;
	
	/** Subtitle or description */
	description?: string;

	/** Items to render in the current table state */
	items: TData[];

	/** Tanstack Column Definitions */
	columns: ColumnDef<TData, any>[];

	/** A SmartPagination instance to hook into backend sorting/filtering */
	pagination?: SmartPagination<TData>;

	/** Is the list loading? */
	loading?: boolean;

	/** Is it fetching the next page? */
	loadingMore?: boolean;

	/** Search input text. Leave empty to not show search */
	searchQuery?: string;

	/** Shows/Hides density toggle */
	showDensity?: boolean;

	/** Shows/Hides view mode (list / table) toggle */
	showViewToggle?: boolean;

	/** Shows/Hides column toggle */
	showColumnToggle?: boolean;

	/** When true, mobile screens default to the DataCard grid */
	mobileCardFallback?: boolean;

	/** Key from the row to render as the Card Title */
	mobileCardTitleKey?: keyof TData;

	/** Key from the row to render as the Card Subtitle */
	mobileCardSubtitleKey?: keyof TData;
	
	/** When a row is clicked */
	onRowClick?: (item: TData) => void;

	/** Render hook for table global actions at top */
	actions?: import("svelte").Snippet;

	/** Shows/Hides generic multi filter button */
	showFilters?: boolean;

	/** Current active filters if controlled externally */
	filterRules?: FilterRule[];

	/** Callback when filter rules array changes */
	onFilterRulesChange?: (rules: FilterRule[]) => void;
}

export type FilterOperator = 'eq' | 'neq' | 'contains' | 'startsWith' | 'endsWith' | 'gt' | 'gte' | 'lt' | 'lte' | 'in' | 'notIn';

export interface FilterRule {
	id: string;
	columnId: string;
	operator: FilterOperator;
	value: any;
}
