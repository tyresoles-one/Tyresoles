import { get } from "svelte/store";
import { page } from "$app/stores";
import { goto } from "$app/navigation";
import { onMount } from "svelte";
import type { Readable } from "svelte/store";

/** Legacy alias used by ecoproc routes (`$pageStore.params`). */
export const pageStore = page;

export function getStore<T>(store: Readable<T>): T {
	return get(store);
}

export { goto, onMount };
