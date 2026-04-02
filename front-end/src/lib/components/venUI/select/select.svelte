<script lang="ts" generics="T">
	import * as Command from "$lib/components/ui/command";
	import * as Popover from "$lib/components/ui/popover";
	import { cn } from "$lib/utils.js";
	import Check from "@lucide/svelte/icons/check";
	import ChevronsUpDown from "@lucide/svelte/icons/chevrons-up-down";
	import X from "@lucide/svelte/icons/x";

	type Props = {
		options: T[];
		value?: any;
		placeholder?: string;
		searchPlaceholder?: string;
		emptyText?: string;
		columns?: {
			header: string;
			accessor: keyof T | ((item: T) => any);
			class?: string;
		}[];
		valueKey?: keyof T;
		labelKey?: keyof T;
		class?: string;
		contentClass?: string;
		disabled?: boolean;
		multiple?: boolean;
		clearable?: boolean;
		showSelectAll?: boolean;
		selectAllLabel?: string;
        onSelect?: (item: T | null) => void;
        "aria-invalid"?: boolean;
	};

	let {
		options = [],
		value = $bindable(),
		placeholder = "Select...",
		searchPlaceholder = "Search...",
		emptyText = "No results found.",
		columns = [],
		valueKey,
		labelKey,
		class: className,
		contentClass,
		disabled = false,
		multiple = false,
		clearable = false,
		showSelectAll = true,
		selectAllLabel = "Select All",
        onSelect,
        "aria-invalid": ariaInvalid,
		...restProps
	}: Props = $props();

	let open = $state(false);

	function getItemValue(item: T) {
		if (valueKey) return item[valueKey];
		return item;
	}

	function getItemLabel(item: T) {
		if (labelKey && item[labelKey] != null) return String(item[labelKey]);
		return String(item);
	}

	function getCellContent(item: T, accessor: keyof T | ((item: T) => any)) {
		if (typeof accessor === "function") return accessor(item);
		return item[accessor];
	}

    // Lazy Loading & Manual Filtering Logic
    let searchQuery = $state("");
    let displayLimit = $state(20);
    const BATCH_SIZE = 20;

    $effect(() => {
        // Reset limit when search changes
        searchQuery;
        displayLimit = BATCH_SIZE;
    });

    let filteredOptions = $derived.by(() => {
        if (!searchQuery) return options;
        const lowerQuery = searchQuery.toLowerCase();
        
        return options.filter(item => {
            // Check label
            if (getItemLabel(item).toLowerCase().includes(lowerQuery)) return true;
            
            // Check columns if in table mode
            if (columns.length > 0) {
                return columns.some(col => {
                    const content = getCellContent(item, col.accessor);
                    return String(content).toLowerCase().includes(lowerQuery);
                });
            }
            return false;
        });
    });

    let visibleOptions = $derived(filteredOptions.slice(0, displayLimit));

    // Simple action to detect when element enters viewport
    function viewport(node: HTMLElement) {
        const observer = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting) {
                if (displayLimit < filteredOptions.length) {
                    displayLimit += BATCH_SIZE;
                }
            }
        }, { root: null, rootMargin: "100px" });

        observer.observe(node);

        return {
            destroy() {
                observer.disconnect();
            }
        };
    }

	let selectedLabel = $derived.by(() => {
		if (multiple) {
			if (!Array.isArray(value) || value.length === 0) return placeholder;
			if (value.length === 1) {
				const selected = options.find((o) => getItemValue(o) === value[0]);
				return selected ? getItemLabel(selected) : placeholder;
			}
			return `${value.length} selected`;
		}
		const selected = options.find((o) => getItemValue(o) === value);
		return selected ? getItemLabel(selected) : placeholder;
	});
	
	let isTableMode = $derived(columns.length > 0);
	
	let areAllSelected = $derived.by(() => {
		if (!multiple || !Array.isArray(value)) return false;
		return options.length > 0 && value.length === options.length;
	});

	function handleSelect(currentValue: any) {
		if (multiple) {
			if (!Array.isArray(value)) value = [];
			const index = value.indexOf(currentValue);
			if (index >= 0) {
				value = value.filter((v: any) => v !== currentValue);
			} else {
				value = [...value, currentValue];
			}
			if (onSelect) {
				const selectedItem = options.find((o) => getItemValue(o) === currentValue);
				onSelect(selectedItem ?? null);
			}
			// Keep open for multiple selection
		} else {
			value = currentValue;
			open = false;
            if (onSelect) {
                const selectedItem = options.find(o => getItemValue(o) === currentValue);
                onSelect(selectedItem || null);
            }
		}
	}

	function handleSelectAll() {
		if (areAllSelected) {
			value = [];
		} else {
			value = options.map((o) => getItemValue(o));
		}
		if (onSelect) onSelect(null);
	}

	function handleClear(e: MouseEvent) {
		e.stopPropagation();
		value = multiple ? [] : undefined;
        if (onSelect) onSelect(null);
	}
</script>

<Popover.Root bind:open>
  <Popover.Trigger
    class={cn(
      "border-input ring-offset-background placeholder:text-muted-foreground focus:ring-ring flex h-9 w-full items-center justify-between whitespace-nowrap rounded-md border bg-transparent px-3 py-2 text-sm shadow-xs focus:outline-none focus:ring-1 disabled:cursor-not-allowed disabled:opacity-50 [&>span]:line-clamp-1",
      "aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 focus:aria-invalid:ring-[3px]",
      className,
    )}
    {disabled}
    aria-invalid={ariaInvalid}
    {...restProps}
  >
    <span class="truncate">
      {selectedLabel}
    </span>
    <div class="ml-2 flex flex-shrink-0 items-center gap-2">
      {#if clearable && ((multiple && Array.isArray(value) && value.length > 0) || (!multiple && value))}
        <button
          tabindex="-1"
          data-ven-form-slot
          type="button"
          class="hover:text-foreground text-muted-foreground rounded-sm opacity-60 transition-colors hover:opacity-100 outline-none ring-0 focus:outline-none focus-visible:outline-none focus-visible:ring-0"
          onclick={(e) => handleClear(e)}
        >
          <X class="size-4" />
        </button>
      {/if}
      <ChevronsUpDown class="size-4 opacity-50" />
    </div>
  </Popover.Trigger>
  <Popover.Content
    class={cn(
      "w-[var(--bits-popover-anchor-width)] min-w-[200px] p-0",
      "max-w-[calc(100vw-2rem)]",
      contentClass
    )}
    align="start"
    sideOffset={4}
  >
    <Command.Root shouldFilter={false} class="flex flex-col h-full max-h-[min(80vh,400px)]">
      <Command.Input placeholder={searchPlaceholder} bind:value={searchQuery} />
      <Command.List class="overflow-x-hidden overflow-y-auto flex-1 h-full max-h-none">
        <Command.Empty>{emptyText}</Command.Empty>
        {#if multiple && showSelectAll && options.length > 0 && !isTableMode}
          <Command.Group class="overflow-visible min-w-full w-max border-b">
            <Command.Item
              value="--select-all--"
              onSelect={handleSelectAll}
              class={cn(
                "cursor-pointer pr-4 font-medium min-w-full w-max",
                isTableMode
                  ? "flex items-center gap-4"
                  : "flex items-center gap-2",
              )}
            >
              <div class="flex items-center justify-center w-4 shrink-0">
                <Check
                  class={cn(
                    "size-4",
                    areAllSelected ? "opacity-100" : "opacity-0",
                  )}
                />
              </div>
              <span>{selectAllLabel}</span>
            </Command.Item>
          </Command.Group>
        {/if}
        {#if isTableMode}
          <div
            class="text-muted-foreground border-border bg-popover sticky top-0 z-10 flex min-w-full w-max items-center gap-4 border-b px-2 py-2 text-xs font-medium pr-4"
          >
            <!-- Spacer for check icon OR Select All toggle -->
            <div class="flex items-center justify-center w-4 shrink-0">
              {#if multiple && showSelectAll}
                <button
                  type="button"
                  onclick={handleSelectAll}
                  class="flex cursor-pointer items-center justify-center p-0 transition-opacity hover:opacity-100 focus:outline-none"
                >
                  <Check
                    class={cn(
                      "size-4",
                      areAllSelected ? "opacity-100" : "opacity-40",
                    )}
                  />
                </button>
              {:else}
                <div class="w-4 shrink-0"></div>
              {/if}
            </div>
            {#each columns as col}
              <!-- Apply flex-1 by default but allow overriding via col.class -->
              <div class={cn("truncate flex-1", col.class)}>{col.header}</div>
            {/each}
          </div>
        {/if}
        <Command.Group class="overflow-visible min-w-full w-max">
          {#each visibleOptions as item}
            {@const itemValue = getItemValue(item)}
            {@const isSelected = multiple
              ? Array.isArray(value) && value.includes(itemValue)
              : value === itemValue}
            <Command.Item
              value={String(itemValue)}
              keywords={[]}
              onSelect={() => handleSelect(itemValue)}
              class={cn(
                "cursor-pointer pr-4 min-w-full w-max",
                isTableMode
                  ? "flex items-center gap-4"
                  : "flex items-center gap-2",
              )}
            >
              <div class="flex items-center justify-center w-4 shrink-0">
                <Check
                  class={cn("size-4", isSelected ? "opacity-100" : "opacity-0")}
                />
              </div>

              {#if isTableMode}
                {#each columns as col}
                  <div class={cn("truncate flex-1", col.class)}>
                    {getCellContent(item, col.accessor)}
                  </div>
                {/each}
              {:else}
                {getItemLabel(item)}
              {/if}
            </Command.Item>
          {/each}
          <!-- Loader Trigger -->
          {#if displayLimit < filteredOptions.length}
            <div use:viewport class="h-4 w-full"></div>
          {/if}
        </Command.Group>
      </Command.List>
    </Command.Root>
  </Popover.Content>
</Popover.Root>
