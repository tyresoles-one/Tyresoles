<script lang="ts">
	import * as Avatar from '$lib/components/ui/avatar';
	import * as Popover from '$lib/components/ui/popover';
	import { Icon } from '$lib/components/venUI/icon';
	import { cn } from '$lib/utils';
	import { getImages } from '$lib/config/system';

	interface Props {
		/** The index of the selected avatar from the avatar images array */
		selectedIndex?: number;
		/** Maximum number of images to show in the selector */
		maxImages?: number;
		onValueChange?: (index: number) => void;
	}

	let {
		selectedIndex = $bindable(0),
		maxImages = 8,
		onValueChange
	}: Props = $props();

	let popoverOpen = $state(false);

	const images = $derived(getImages());

	/** Clamp avatar index to valid images range. */
	function clampAvatarIndex(index: number): number {
		if (images.length === 0) return 0;
		return Math.max(0, Math.min(index, images.length - 1));
	}

	function selectAvatar(index: number) {
		selectedIndex = clampAvatarIndex(index);
		popoverOpen = false;
		onValueChange?.(selectedIndex);
	}

	const previewImage = $derived(images[clampAvatarIndex(selectedIndex)]);
</script>

<div class="space-y-6 flex flex-col items-center ">	

	<Popover.Root bind:open={popoverOpen}>
		<Popover.Trigger class="group w-full h-auto p-4 bg-transparent hover:bg-muted/50 border border-transparent hover:border-border/60 transition-all rounded-3xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2">
			<div class="flex flex-col items-center gap-4 w-full">
				<div class="relative">
					<Avatar.Root
						class="size-28 sm:size-32 rounded-full border-2 border-border shadow-sm ring-4 ring-primary/10 transition-transform duration-300 group-hover:scale-105 group-hover:shadow-md bg-background"
						aria-label="Selected avatar"
					>
						<Avatar.Image
							src={previewImage?.url}
							alt={previewImage?.name ?? 'Avatar'}
							class="rounded-full object-cover"
						/>
						<Avatar.Fallback
							class="rounded-full bg-muted text-muted-foreground text-4xl"
						>
							<Icon name="user" class="size-14" />
						</Avatar.Fallback>
					</Avatar.Root>
					
					<div class="absolute bottom-0 right-0 p-2 bg-primary text-primary-foreground rounded-full shadow-md border-2 border-background transition-transform duration-300 group-hover:scale-110">
						<Icon name="pencil" class="size-4" />
					</div>
				</div>			
							</div>
		</Popover.Trigger>
		<Popover.Content class="w-auto p-4 rounded-3xl shadow-xl border-border/80 bg-background/95 backdrop-blur-md -mt-2">
			<p class="text-xs font-semibold text-muted-foreground mb-4 text-center uppercase tracking-wide">Available Avatars</p>
			<div class="grid grid-cols-4 gap-3 sm:gap-4">
				{#each images.slice(0, maxImages) as image, index}
					<button
						type="button"
						onclick={() => selectAvatar(index)}
						title={image.name.replace(/-/g, ' ')}
						class={cn(
							"relative group flex items-center justify-center rounded-full transition-all duration-300 outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2",
							selectedIndex === index 
								? "scale-110 z-10" 
								: "hover:scale-110 hover:z-10 opacity-70 hover:opacity-100"
						)}
					>
						<div class={cn(
							"size-14 sm:size-16 rounded-full overflow-hidden transition-all duration-300",
							selectedIndex === index
								? "ring-2 ring-primary ring-offset-2 ring-offset-background shadow-lg"
								: "ring-1 ring-border shadow-sm group-hover:shadow-md group-hover:ring-primary/40"
						)}>
							<img 
								src={image.url} 
								alt={image.name} 
								class="size-full object-cover bg-background"
								loading="lazy"
							/>
						</div>
						
						{#if selectedIndex === index}
							<div class="absolute -bottom-1 -right-1 bg-primary text-primary-foreground rounded-full p-1 shadow-sm border-2 border-background animate-in zoom-in duration-200">
								<Icon name="check" class="size-2.5 sm:size-3" strokeWidth={3} />
							</div>
						{/if}
					</button>
				{/each}
			</div>
		</Popover.Content>
	</Popover.Root>
</div>
