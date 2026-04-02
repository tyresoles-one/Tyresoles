import { writable } from "svelte/store";

/** Path used by layouts / back navigation for ecoproc legacy flows. */
export const goBackPathStore = writable<string | null>(null);

export function updateGoBackPath(path: string) {
	goBackPathStore.set(path);
}
