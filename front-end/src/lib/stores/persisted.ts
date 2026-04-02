/**
 * Generic persisted store: Svelte store that syncs to localStorage.
 * - Debounced writes: batches rapid updates to avoid blocking the main thread.
 * - Lazy read: reads from localStorage once on init (client-only).
 * - SSR-safe: no localStorage on server; store still works with initial value.
 * - Full store contract: subscribe, set, update; optional get() for sync read.
 */

import { writable } from 'svelte/store';

const isClient = typeof window !== 'undefined';

export type PersistedStoreOptions<T> = {
	/** Debounce delay in ms before writing to localStorage (default 50). */
	debounceMs?: number;
	/** Key prefix for namespacing (e.g. "app" → key "app:user"). */
	prefix?: string;
	/** Custom storage (default localStorage). Use sessionStorage or a mock in tests. */
	storage?: Storage;
	/** Custom serialize (default JSON.stringify). Use for Date, Map, etc. */
	serialize?: (value: T) => string;
	/** Custom deserialize (default JSON.parse). Must match serialize. */
	deserialize?: (raw: string) => T;
};

type JsonPrimitive = string | number | boolean | null;
type JsonValue = JsonPrimitive | { [key: string]: JsonValue } | JsonValue[];
export type PersistedValue = object | JsonPrimitive;

function defaultSerialize<T>(value: T): string {
	return JSON.stringify(value);
}

function defaultDeserialize<T>(raw: string): T {
	return JSON.parse(raw) as T;
}

function resolveKey(key: string, prefix?: string): string {
	if (prefix) return `${prefix}:${key}`;
	return key;
}

/**
 * Creates a Svelte store that persists to localStorage.
 * Writes are debounced; reads from storage happen once on first init (client-side).
 *
 * @example
 * ```ts
 * const userStore = createPersistedStore('user', { name: '', token: '' });
 * // In Svelte: $userStore, userStore.set(...), userStore.update(...)
 * ```
 *
 * @example With options
 * ```ts
 * const prefs = createPersistedStore('prefs', { theme: 'light' }, {
 *   debounceMs: 100,
 *   prefix: 'myapp',
 * });
 * ```
 */
export function createPersistedStore<T extends PersistedValue>(
	key: string,
	initial: T,
	options: PersistedStoreOptions<T> = {}
): {
	subscribe: (run: (value: T) => void, invalidate?: (value?: T) => void) => () => void;
	set: (value: T) => void;
	update: (fn: (value: T) => T) => void;
	/** Current value without subscribing. Use for one-off reads. */
	get: () => T;
} {
	const {
		debounceMs = 50,
		prefix,
		storage = isClient ? window.localStorage : (undefined as unknown as Storage),
		serialize = defaultSerialize,
		deserialize = defaultDeserialize,
	} = options;

	const fullKey = resolveKey(key, prefix);

	let current: T = initial;
	let flushTimer: ReturnType<typeof setTimeout> | null = null;
	let pending: T | null = null;

	function read(): T | null {
		if (!isClient || !storage) return null;
		try {
			const raw = storage.getItem(fullKey);
			if (raw == null) return null;
			return deserialize(raw) as T;
		} catch {
			return null;
		}
	}

	function write(value: T): void {
		if (!isClient || !storage) return;
		try {
			storage.setItem(fullKey, serialize(value));
		} catch {
			// Quota or security; ignore
		}
	}

	function flush(): void {
		if (flushTimer != null) {
			clearTimeout(flushTimer);
			flushTimer = null;
		}
		if (pending !== null) {
			write(pending);
			pending = null;
		}
	}

	function scheduleWrite(value: T): void {
		pending = value;
		if (flushTimer == null) {
			flushTimer = setTimeout(flush, debounceMs);
		}
	}

	// Hydrate from storage once on client (lazy: only when store is created in browser)
	if (isClient && storage) {
		const stored = read();
		if (stored != null) current = stored;
	}

	const { subscribe, set: writableSet } = writable(current);

	// Flush on page unload so last value is not lost (then remove listener)
	if (isClient) {
		window.addEventListener('beforeunload', function onBeforeUnload() {
			flush();
			window.removeEventListener('beforeunload', onBeforeUnload);
		});
	}

	function setValue(value: T): void {
		current = value;
		writableSet(value);
		scheduleWrite(value);
	}

	return {
		subscribe,

		set(value: T) {
			setValue(value);
		},

		update(fn: (value: T) => T) {
			setValue(fn(current));
		},

		get() {
			return current;
		},
	};
}
