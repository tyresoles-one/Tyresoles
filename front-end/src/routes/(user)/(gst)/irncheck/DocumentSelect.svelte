<script lang="ts">
    import { untrack } from 'svelte';
    import * as Command from '$lib/components/ui/command';
    import * as Popover from '$lib/components/ui/popover';
    import Check from '@lucide/svelte/icons/check';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import Loader2 from '@lucide/svelte/icons/loader-2';
    import X from '@lucide/svelte/icons/x';
    import { cn } from '$lib/utils';
    import * as Field from '$lib/components/ui/field';
    import * as api from './api';
    import type { Document } from './api';

    type Props = {
        form: { 
            values: Record<string, unknown>; 
            setTouched: (name: string) => void;
            setValue: (name: string, value: any) => void;
            errors?: Record<string, string | undefined>;
        };
        fieldName: string;
        label?: string;
        placeholder?: string;
        disabled?: boolean;
        orientation?: 'vertical' | 'horizontal' | 'responsive';
        docType: string;
        respCenter: string;
    };

    let { form, fieldName, label, placeholder = 'Search Document No...', disabled = false, orientation = 'vertical', docType, respCenter }: Props = $props();

    let open = $state(false);
    let searchQuery = $state('');
    let debouncedSearch = $state('');
    let documents = $state<Document[]>([]);
    let loading = $state(false);
    let loadingMore = $state(false);
    let hasMore = $state(true);
    let skip = $state(0);
    const take = 20;

    const valueStr = $derived(String(form.values[fieldName] ?? ''));

    // Debounce search (300ms)
    $effect(() => {
        const q = searchQuery;
        const t = setTimeout(() => {
            debouncedSearch = q;
        }, 300);
        return () => clearTimeout(t);
    });

    async function fetchDocuments(append: boolean) {
        if (!docType || !respCenter) return;
        
        if (append) {
            loadingMore = true;
        } else {
            loading = true;
            skip = 0;
            hasMore = true;
        }

        try {
            const res = await api.fetchDocuments(docType, [respCenter], debouncedSearch, skip, take);
            if (append) {
                documents = [...documents, ...res];
            } else {
                documents = res;
            }
            hasMore = res.length === take;
            skip += take;
        } catch (err) {
            console.error("Failed to fetch documents", err);
        } finally {
            loading = false;
            loadingMore = false;
        }
    }

    let lastFetchParams = $state('');
    let loadingCountValue = 0;

    // Refetch when docType, respCenter or debouncedSearch changes
    $effect(() => {
        if (!open) return;
        
        // Track these signals
        const currentParams = `${docType}|${respCenter}|${debouncedSearch}`;
        
        if (currentParams !== lastFetchParams) {
            lastFetchParams = currentParams;
            untrack(() => {
                fetchPage(false);
            });
        }
    });

    function fetchPage(append: boolean) {
         fetchDocuments(append);
    }

    // Scroll-to-bottom: load more
    function viewport(node: HTMLElement) {
        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting && hasMore && !loadingMore && !loading) {
                    fetchPage(true);
                }
            },
            { root: null, rootMargin: '80px' }
        );
        observer.observe(node);
        return { destroy: () => observer.disconnect() };
    }

    function toggleSelection(val: string) {
        form.setValue(fieldName, val);
        form.setTouched(fieldName);
        open = false;
        searchQuery = '';
    }

    function handleClear(e: MouseEvent) {
        e.stopPropagation();
        form.setValue(fieldName, '');
        form.setTouched(fieldName);
    }

    const selectedLabel = $derived(
        valueStr === ''
            ? placeholder
            : valueStr
    );
    const error = $derived(form.errors?.[fieldName]);
</script>

<Field.Field {orientation} class="w-full" data-invalid={!!error}>
    {#if label}
        <Field.Label class={cn(
            (orientation === 'horizontal' || orientation === 'responsive') && "flex-none sm:w-32 md:w-40"
        )}>
            {label}
        </Field.Label>
    {/if}
    <Field.Content>
    <Popover.Root bind:open>
        <Popover.Trigger
            class={cn(
                'border-input ring-offset-background placeholder:text-muted-foreground focus:ring-ring flex h-9 w-full items-center justify-between whitespace-nowrap rounded-md border bg-transparent px-3 py-2 text-sm shadow-xs focus:outline-none focus:ring-1 disabled:cursor-not-allowed disabled:opacity-50 [&>span]:line-clamp-1',
                'aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 focus:aria-invalid:ring-[3px]'
            )}
            disabled={disabled || !docType || !respCenter}
            aria-invalid={!!error}
        >
            <span class="truncate">{selectedLabel}</span>
            <div class="ml-2 flex shrink-0 items-center gap-2">
                {#if valueStr !== ''}
                    <button
                        type="button"
                        class="hover:text-foreground text-muted-foreground rounded-sm opacity-60 transition-colors hover:opacity-100 focus:outline-none"
                        onclick={handleClear}
                    >
                        <X class="size-4" />
                    </button>
                {/if}
                <ChevronsUpDown class="size-4 opacity-50" />
            </div>
        </Popover.Trigger>
        <Popover.Content
            class="min-w-[200px] p-0 max-w-[calc(100vw-2rem)] w-(--bits-popover-anchor-width)"
            align="start"
            sideOffset={4}
        >
            <Command.Root shouldFilter={false} class="flex flex-col max-h-[min(80vh,400px)]">
                <Command.Input placeholder="Search document..." bind:value={searchQuery} />
                <Command.List class="overflow-x-hidden overflow-y-auto flex-1 max-h-none min-h-[120px]">
                    {#if loading}
                        <div class="flex items-center justify-center py-8 text-muted-foreground">
                            <Loader2 class="size-5 animate-spin" />
                        </div>
                    {:else}
                        {#if documents.length === 0}
                            <Command.Empty>No documents found.</Command.Empty>
                        {/if}
                        <Command.Group class="overflow-visible min-w-full w-max">
                            {#each documents as doc}
                                {@const isSelected = valueStr === doc.no}
                                <Command.Item
                                    value={doc.no}
                                    keywords={[]}
                                    onSelect={() => toggleSelection(doc.no)}
                                    class="cursor-pointer pr-4 min-w-full w-max flex items-center gap-2"
                                >
                                    <div class="flex h-4 w-4 shrink-0 items-center justify-center">
                                        <Check class={cn('size-4', isSelected ? 'opacity-100' : 'opacity-0')} />
                                    </div>
                                    <div class="flex flex-col">
                                        <span class="font-bold">{doc.no}</span>
                                        <span class="text-[10px] text-muted-foreground">{doc.name} - {new Date(doc.date).toLocaleDateString()}</span>
                                    </div>
                                </Command.Item>
                            {/each}
                            {#if hasMore && documents.length > 0}
                                <div use:viewport class="h-8 flex items-center justify-center border-t border-border/40 mt-2">
                                    {#if loadingMore}
                                        <Loader2 class="size-4 animate-spin text-muted-foreground" />
                                    {:else}
                                        <span class="text-muted-foreground text-xs font-medium">Loading more...</span>
                                    {/if}
                                </div>
                            {/if}
                        </Command.Group>
                    {/if}
                </Command.List>
            </Command.Root>
        </Popover.Content>
    </Popover.Root>
    </Field.Content>
    {#if error}
        <Field.Error>{error}</Field.Error>
    {/if}
</Field.Field>
