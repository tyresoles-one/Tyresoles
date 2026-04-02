export type SortDirection = 'asc' | 'desc';

export interface ListConfig<T> {
	filter?: (item: T, query: string) => boolean;
	defaultSortField?: keyof T | string;
	defaultSortDirection?: SortDirection;
}

export function createList<T>(
	getItems: () => T[], 
	config: ListConfig<T> = {}
) {
	let searchQuery = $state('');
	let sortField = $state<keyof T | string>(config.defaultSortField || '');
	let sortDirection = $state<SortDirection>(config.defaultSortDirection || 'asc');

	const filtered = $derived.by(() => {
		let result = [...getItems()];

		// 1. Filter
		if (searchQuery.trim() && config.filter) {
			result = result.filter(item => config.filter!(item, searchQuery));
		}

		// 2. Sort
		if (sortField) {
			result.sort((a, b) => {
				// eslint-disable-next-line @typescript-eslint/no-explicit-any
				const aItem = (a as any)[sortField];
				// eslint-disable-next-line @typescript-eslint/no-explicit-any
				const bItem = (b as any)[sortField];

				// Handle numeric sort
				if (typeof aItem === 'number' && typeof bItem === 'number') {
					return sortDirection === 'asc' ? aItem - bItem : bItem - aItem;
				}

				// Fallback to string sort
				const aValue = String(aItem || '').toLowerCase();
				const bValue = String(bItem || '').toLowerCase();

				if (aValue < bValue) return sortDirection === 'asc' ? -1 : 1;
				if (aValue > bValue) return sortDirection === 'asc' ? 1 : -1;
				return 0;
			});
		}

		return result;
	});

	function toggleSort(field: keyof T | string) {
		if (sortField === field) {
			sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
		} else {
			sortField = field;
			sortDirection = 'asc';
		}
	}

	return {
		get items() { return filtered; },
		get searchQuery() { return searchQuery; },
		set searchQuery(v) { searchQuery = v; },
		get sortField() { return sortField; },
		get sortDirection() { return sortDirection; },
		toggleSort
	};
}
