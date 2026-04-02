<script lang="ts">
	import { page } from '$app/stores';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { secureFetch } from '$lib/services/api';
	import { toast } from '$lib/components/venUI/toast';
	import { cn } from '$lib/utils';

	const dealerCode = $derived(decodeURIComponent($page.params.id ?? '').trim());
	const backToDealerHref = $derived(
		dealerCode ? `/dealers/${encodeURIComponent(dealerCode)}` : '/dealers'
	);

	let docType = $state(1);
	let loading = $state(true);
	let uploading = $state(false);
	let loadError = $state('');

	type DocItem = { lineNo: number; imageBase64: string };
	let items = $state<DocItem[]>([]);

	type Pending = { name: string; dataUrl: string; base64: string };
	let pending = $state<Pending[]>([]);

	let fileInputEl = $state<HTMLInputElement | undefined>(undefined);

	function navDocType(): number {
		const n = Number(docType);
		return Number.isFinite(n) && n >= 1 ? Math.floor(n) : 1;
	}

	function dataUrlFromBase64(b64: string): string {
		if (!b64) return '';
		return `data:image/jpeg;base64,${b64}`;
	}

	async function loadImages() {
		if (!dealerCode) {
			loading = false;
			return;
		}
		loading = true;
		loadError = '';
		try {
			const res = await secureFetch(
				`/api/dealers/${encodeURIComponent(dealerCode)}/documents?docType=${navDocType()}`
			);
			if (!res.ok) {
				const j = (await res.json().catch(() => ({}))) as { error?: string };
				throw new Error(j.error ?? res.statusText);
			}
			const data = (await res.json()) as { items?: DocItem[] };
			items = data.items ?? [];
		} catch (e) {
			loadError = e instanceof Error ? e.message : 'Failed to load images';
			items = [];
		} finally {
			loading = false;
		}
	}

	$effect(() => {
		// reload when dealer or doc type changes
		dealerCode;
		docType;
		void loadImages();
	});

	/**
	 * Compresses an image data URL using a Canvas.
	 * Max width/height of 1600px and 0.85 quality for a balance of speed and clarity.
	 */
	async function compressImage(dataUrl: string, maxWidth = 1600, maxHeight = 1600, quality = 0.85): Promise<string> {
		return new Promise((resolve) => {
			const img = new Image();
			img.onload = () => {
				let { width, height } = img;
				if (width > maxWidth) {
					height *= maxWidth / width;
					width = maxWidth;
				}
				if (height > maxHeight) {
					width *= maxHeight / height;
					height = maxHeight;
				}
				const canvas = document.createElement('canvas');
				canvas.width = width;
				canvas.height = height;
				const ctx = canvas.getContext('2d');
				ctx?.drawImage(img, 0, 0, width, height);
				resolve(canvas.toDataURL('image/jpeg', quality));
			};
			img.onerror = () => resolve(dataUrl); // Fallback to raw if logic fails
			img.src = dataUrl;
		});
	}

	async function onFilesSelected(e: Event) {
		const input = e.currentTarget as HTMLInputElement;
		const files = input.files;
		if (!files?.length) return;

		const next: Pending[] = [];
		for (const file of Array.from(files)) {
			if (!file.type.startsWith('image/')) continue;
			try {
				const rawUrl = await new Promise<string>((resolve, reject) => {
					const r = new FileReader();
					r.onload = () => resolve(String(r.result ?? ''));
					r.onerror = () => reject(new Error('Could not read file'));
					r.readAsDataURL(file);
				});
				
				// Compress to reduce network bandwidth and avoid server size limits
				const dataUrl = await compressImage(rawUrl);
				const base64 = dataUrl.includes('base64,') ? (dataUrl.split('base64,')[1] ?? '') : '';
				next.push({ name: file.name, dataUrl, base64 });
			} catch (err) {
				console.error('File read error:', err);
			}
		}
		pending = [...pending, ...next];
		input.value = '';
	}

	function removePending(i: number) {
		pending = pending.filter((_, j) => j !== i);
	}

	async function uploadPending() {
		if (!dealerCode || pending.length === 0) return;
		uploading = true;
		try {
			const res = await secureFetch(`/api/dealers/${encodeURIComponent(dealerCode)}/documents`, {
				method: 'POST',
				body: JSON.stringify({
					docType: navDocType(),
					images: pending.map((p) => p.base64)
				})
			});
			const j = (await res.json().catch(() => ({}))) as { error?: string };
			if (!res.ok) throw new Error(j.error ?? 'Upload failed');
			toast.success('Images uploaded');
			pending = [];
			await loadImages();
		} catch (e) {
			toast.error(e instanceof Error ? e.message : 'Upload failed');
		} finally {
			uploading = false;
		}
	}

	function triggerPick() {
		fileInputEl?.click();
	}
</script>

<div class="min-h-screen bg-background pb-12">
	<PageHeading backHref={backToDealerHref} backLabel="Back to dealer" loading={loading} icon="image">
		{#snippet title()}
			Dealer documents
		{/snippet}
		{#snippet description()}
			{#if dealerCode}
				<span class="font-mono text-xs">{dealerCode}</span>
			{:else}
				Upload images linked to this dealer
			{/if}
		{/snippet}
	</PageHeading>

	<main class="mx-auto w-full max-w-5xl px-3 py-4 sm:px-4 sm:py-6">
		{#if !dealerCode}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">No dealer code in the URL.</p>
				</CardContent>
			</Card>
		{:else}
			<div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
				<div class="space-y-1.5 w-full sm:max-w-xs">
					<label for="doc-type" class="text-xs font-medium text-muted-foreground">Document type (NAV)</label>
					<Input
						id="doc-type"
						type="number"
						min={1}
						bind:value={docType}
						class="h-10 text-sm"
					/>
				</div>
				<div class="flex flex-col gap-2 w-full sm:w-auto sm:flex-row sm:items-center">
					<input
						bind:this={fileInputEl}
						type="file"
						accept="image/*"
						multiple
						class="sr-only"
						aria-hidden="true"
						onchange={onFilesSelected}
					/>
					<Button
						type="button"
						variant="outline"
						class="w-full min-h-11 touch-manipulation sm:w-auto"
						onclick={triggerPick}
						disabled={uploading}
					>
						<Icon name="upload" class="size-4 mr-2 shrink-0" />
						Choose images
					</Button>
					<Button
						type="button"
						class="w-full min-h-11 touch-manipulation sm:w-auto"
						disabled={uploading || pending.length === 0}
						onclick={uploadPending}
					>
						{#if uploading}
							<Icon name="loader-2" class="size-4 mr-2 animate-spin shrink-0" />
							Uploading…
						{:else}
							<Icon name="upload" class="size-4 mr-2 shrink-0" />
							Upload {pending.length ? `(${pending.length})` : ''}
						{/if}
					</Button>
				</div>
			</div>

			{#if pending.length > 0}
				<Card class="mt-4 border-border/50 shadow-sm">
					<CardHeader class="pb-2 px-4 pt-4 sm:px-6">
						<CardTitle class="text-base">Ready to upload</CardTitle>
						<CardDescription class="text-xs">Review thumbnails, then upload.</CardDescription>
					</CardHeader>
					<CardContent class="px-4 pb-4 sm:px-6">
						<ul
							class="grid grid-cols-2 gap-2 sm:grid-cols-3 sm:gap-3 md:grid-cols-4"
							role="list"
						>
							{#each pending as p, i (p.name + i)}
								<li class="group relative">
									<div
										class="aspect-square overflow-hidden rounded-lg border border-border/60 bg-muted/30"
									>
										<img
											src={p.dataUrl}
											alt={p.name}
											class="h-full w-full object-cover"
										/>
									</div>
									<p class="mt-1 truncate text-[11px] text-muted-foreground sm:text-xs">{p.name}</p>
									<Button
										type="button"
										variant="secondary"
										size="sm"
										class="absolute right-1 top-1 h-8 w-8 min-h-8 p-0 opacity-90 sm:opacity-0 sm:group-hover:opacity-100"
										onclick={() => removePending(i)}
										aria-label="Remove"
									>
										<Icon name="x" class="size-4" />
									</Button>
								</li>
							{/each}
						</ul>
					</CardContent>
				</Card>
			{/if}

			<Card class="mt-6 border-border/50 shadow-sm">
				<CardHeader class="px-4 pt-4 sm:px-6">
					<CardTitle class="text-base">Stored images</CardTitle>
					<CardDescription class="text-xs">
						Images already saved for this dealer code and document type.
					</CardDescription>
				</CardHeader>
				<CardContent class="px-4 pb-6 sm:px-6">
					{#if loading}
						<div class="grid grid-cols-2 gap-2 sm:grid-cols-3 sm:gap-3 md:grid-cols-4">
							{#each Array(4) as _}
								<Skeleton class="aspect-square w-full rounded-lg" />
							{/each}
						</div>
					{:else if loadError}
						<p class="text-sm text-destructive" role="alert">{loadError}</p>
						<Button variant="outline" size="sm" class="mt-3 min-h-10" onclick={() => loadImages()}>
							Retry
						</Button>
					{:else if items.length === 0}
						<p class="text-sm text-muted-foreground">No images yet. Choose files above to add some.</p>
					{:else}
						<ul
							class="grid grid-cols-2 gap-2 sm:grid-cols-3 sm:gap-3 md:grid-cols-4"
							role="list"
						>
							{#each items as it (it.lineNo)}
								<li>
									<div
										class={cn(
											'aspect-square overflow-hidden rounded-lg border border-border/60 bg-muted/20',
											!it.imageBase64 && 'flex items-center justify-center'
										)}
									>
										{#if it.imageBase64}
											<img
												src={dataUrlFromBase64(it.imageBase64)}
												alt="Line {it.lineNo}"
												class="h-full w-full object-cover"
												loading="lazy"
											/>
										{:else}
											<Icon name="image-off" class="size-8 text-muted-foreground" />
										{/if}
									</div>
									<p class="mt-1 text-center text-[11px] text-muted-foreground sm:text-xs">
										Line {it.lineNo}
									</p>
								</li>
							{/each}
						</ul>
					{/if}
				</CardContent>
			</Card>
		{/if}
	</main>
</div>
