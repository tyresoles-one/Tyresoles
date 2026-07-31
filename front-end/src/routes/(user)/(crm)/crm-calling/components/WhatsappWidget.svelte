<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import Select from '$lib/components/venUI/select/select.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery } from '$lib/services/graphql';
	import { 
		GetCrmWhatsappImagesDocument, 
		GetCrmWhatsappTemplatesDocument,
		GetCrmProductsDocument,
		GetCrmSettingDocument,
		GetCrmProductPriceDocument,
		type CrmWhatsappImage, 
		type CrmWhatsappTemplate,
		type CrmProductItem,
		type CrmContact 
	} from '../queries';

	let {
		selectedContact
	}: {
		selectedContact: CrmContact | null;
	} = $props();

	type PriceGroupMapping = {
		id: string;
		priceGroupCode: string;
		respCenters: string[];
	};

	// State definitions
	let imageSourceType = $state<'product' | 'local'>('product');
	let selectedProductCode = $state<string>('');
	let selectedTemplateId = $state<string>('');
	
	let crmProducts = $state<CrmProductItem[]>([]);
	let preSavedImages = $state<CrmWhatsappImage[]>([]);
	let preSavedTemplates = $state<CrmWhatsappTemplate[]>([]);
	let priceGroupMappings = $state<PriceGroupMapping[]>([]);
	
	let fetchedPrice = $state<number | null>(null);
	let fetchingPrice = $state(false);
	let loadingWhatsappData = $state(false);

	let whatsappImage = $state<File | null>(null);
	let whatsappImagePreview = $state<string>('');
	let whatsappCaption = $state('');
	let isCopyingWhatsapp = $state(false);

	// Deduplicated CRM product options for select dropdown
	let productOptions = $derived.by(() => {
		const map = new Map<string, CrmProductItem>();
		for (const p of crmProducts) {
			if (p.code && !map.has(p.code)) {
				map.set(p.code, p);
			}
		}
		return Array.from(map.values()).map(p => {
			const labelParts = [p.code];
			if (p.category) labelParts.push(p.category);
			if (p.productGroup) labelParts.push(`(${p.productGroup})`);
			return {
				value: p.code,
				label: labelParts.join(' ')
			};
		});
	});

	let templateOptions = $derived(preSavedTemplates.map(t => ({ value: t.id, label: `${t.name} (${t.language})` })));

	let currentPriceGroupCode = $derived.by(() => {
		if (!selectedContact?.respCenter || priceGroupMappings.length === 0) return '';
		const contactRc = selectedContact.respCenter.trim().toLowerCase();
		const match = priceGroupMappings.find(m => 
			m.respCenters?.some(rc => rc.trim().toLowerCase() === contactRc)
		);
		return match ? match.priceGroupCode : '';
	});

	async function loadWhatsappData() {
		loadingWhatsappData = true;
		try {
			const [productsRes, imagesRes, templatesRes, settingRes] = await Promise.all([
				graphqlQuery<{ products: CrmProductItem[] }>(GetCrmProductsDocument, {
					cacheKey: 'crm-products-list',
					cacheTTL: 24 * 60 * 60 * 1000 // 24 hours
				}),
				graphqlQuery<{ images: CrmWhatsappImage[] }>(GetCrmWhatsappImagesDocument, {
					cacheKey: 'crm-whatsapp-images',
					cacheTTL: 24 * 60 * 60 * 1000 // 24 hours
				}),
				graphqlQuery<{ templates: CrmWhatsappTemplate[] }>(GetCrmWhatsappTemplatesDocument, {
					cacheKey: 'crm-whatsapp-templates',
					cacheTTL: 24 * 60 * 60 * 1000 // 24 hours
				}),
				graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(GetCrmSettingDocument, {
					variables: { key: 'CUSTOMER_PRICE_GROUP_MAPPING' }
				})
			]);

			if (productsRes.success && productsRes.data) {
				crmProducts = productsRes.data.products;
			}
			if (imagesRes.success && imagesRes.data) {
				preSavedImages = imagesRes.data.images;
			}
			if (templatesRes.success && templatesRes.data) {
				preSavedTemplates = templatesRes.data.templates;
			}
			if (settingRes.success && settingRes.data?.getCrmSetting?.value) {
				try {
					priceGroupMappings = JSON.parse(settingRes.data.getCrmSetting.value);
				} catch (e) {
					console.error('Failed to parse price group mapping', e);
				}
			}
		} catch (err) {
			console.error('Failed to load CRM products or WhatsApp data', err);
		} finally {
			loadingWhatsappData = false;
		}
	}

	onMount(() => {
		loadWhatsappData();
	});

	// Trigger price calculation & image preview when product or contact changes
	$effect(() => {
		const code = selectedProductCode;
		const respCenter = selectedContact?.respCenter;
		const products = crmProducts;
		const images = preSavedImages;

		untrack(() => {
			if (!code) {
				fetchedPrice = null;
				if (imageSourceType === 'product') whatsappImagePreview = '';
				applyTemplatePrice(fetchedPrice);
				return;
			}

			// 1. Fetch exact price for contact's RC
			fetchItemPrice(code, respCenter || null);

			// 2. Auto-match product image if in product mode
			if (imageSourceType === 'product') {
				const prodMatch = products.find(p => p.code === code);
				let matchedImg: CrmWhatsappImage | undefined;

				if (prodMatch?.whatsappImageCode) {
					matchedImg = images.find(img => 
						img.name?.toLowerCase() === prodMatch.whatsappImageCode?.toLowerCase() ||
						img.id === prodMatch.whatsappImageCode
					);
				}
				if (!matchedImg) {
					matchedImg = images.find(img => img.products?.split(',').map(s => s.trim().toLowerCase()).includes(code.toLowerCase()));
				}

				if (matchedImg) {
					whatsappImagePreview = matchedImg.base64Data || matchedImg.imageUrl || '';
				} else {
					whatsappImagePreview = '';
				}
			}
		});
	});

	async function fetchItemPrice(itemNo: string, respCenter: string | null) {
		fetchingPrice = true;
		try {
			const res = await graphqlQuery<{ price: number | null }>(GetCrmProductPriceDocument, {
				variables: { itemNo, respCenter }
			});
			if (res.success && res.data?.price != null) {
				fetchedPrice = res.data.price;
			} else {
				// Fallback to local product finalPrice if available
				const localMatch = crmProducts.find(p => p.code === itemNo);
				fetchedPrice = localMatch ? localMatch.finalPrice : null;
			}
		} catch (err) {
			console.error('Failed to fetch product price', err);
			const localMatch = crmProducts.find(p => p.code === itemNo);
			fetchedPrice = localMatch ? localMatch.finalPrice : null;
		} finally {
			fetchingPrice = false;
			applyTemplatePrice(fetchedPrice);
		}
	}

	function formatPrice(price: number | null): string {
		if (price == null) return '';
		return price.toLocaleString('en-IN');
	}

	function applyTemplatePrice(price: number | null) {
		if (!selectedTemplateId) return;
		const found = preSavedTemplates.find(t => t.id === selectedTemplateId);
		if (!found) return;

		let text = found.messageText;
		if (/%price%/i.test(text)) {
			const priceStr = formatPrice(price);
			whatsappCaption = text.replace(/%price%/gi, priceStr);
		}
	}

	function handleProductChange(opt: any) {
		if (opt) {
			selectedProductCode = opt.value;
		} else {
			selectedProductCode = '';
			whatsappImagePreview = '';
		}
	}

	function handleTemplateChange(template: any) {
		if (template) {
			selectedTemplateId = template.value;
			const found = preSavedTemplates.find(t => t.id === template.value);
			if (found) {
				let text = found.messageText;
				if (/%price%/i.test(text)) {
					const priceStr = formatPrice(fetchedPrice);
					text = text.replace(/%price%/gi, priceStr);
				}
				whatsappCaption = text;
			}
		} else {
			selectedTemplateId = '';
			whatsappCaption = '';
		}
	}

	function handleWhatsappImageChange(e: Event) {
		const target = e.target as HTMLInputElement;
		const file = target.files?.[0];
		if (file) {
			if (!file.type.startsWith('image/')) {
				toast.error('Please select an image file.');
				return;
			}
			whatsappImage = file;
			whatsappImagePreview = URL.createObjectURL(file);
		}
	}

	function clearWhatsappImage() {
		if (whatsappImagePreview && imageSourceType === 'local') {
			URL.revokeObjectURL(whatsappImagePreview);
		}
		whatsappImage = null;
		if (imageSourceType === 'local') {
			whatsappImagePreview = '';
		}
	}

	function base64ToBlob(base64: string): Blob {
		const parts = base64.split(';base64,');
		const contentType = parts[0].split(':')[1] || 'image/png';
		const raw = window.atob(parts[1] || parts[0]);
		const rawLength = raw.length;
		const uInt8Array = new Uint8Array(rawLength);
		for (let i = 0; i < rawLength; ++i) {
			uInt8Array[i] = raw.charCodeAt(i);
		}
		return new Blob([uInt8Array], { type: contentType });
	}

	async function fetchUrlAsBlob(url: string): Promise<Blob> {
		const response = await fetch(url);
		if (!response.ok) {
			throw new Error(`Failed to fetch image: ${response.statusText}`);
		}
		return await response.blob();
	}

	function convertToPngBlob(blob: Blob): Promise<Blob> {
		return new Promise((resolve, reject) => {
			const img = new Image();
			img.onload = () => {
				const canvas = document.createElement('canvas');
				canvas.width = img.naturalWidth;
				canvas.height = img.naturalHeight;
				const ctx = canvas.getContext('2d');
				if (!ctx) {
					reject(new Error('Failed to get canvas context'));
					return;
				}
				ctx.drawImage(img, 0, 0);
				canvas.toBlob((blobRes) => {
					if (blobRes) {
						resolve(blobRes);
					} else {
						reject(new Error('Failed to convert canvas to blob'));
					}
				}, 'image/png');
			};
			img.onerror = () => reject(new Error('Failed to load image'));
			img.src = URL.createObjectURL(blob);
		});
	}

	function formatWhatsAppNumber(phone: string) {
		if (!phone) return '';
		const clean = phone.replace(/\D/g, '');
		if (clean.length === 10) {
			return '91' + clean;
		}
		return clean;
	}

	function openWhatsappUrl() {
		if (!selectedContact?.mobileNo) return;
		const phone = formatWhatsAppNumber(selectedContact.mobileNo);
		const text = encodeURIComponent(whatsappCaption.trim() || 'Please check the details.');
		const waUrl = `https://api.whatsapp.com/send?phone=${phone}&text=${text}`;
		window.open(waUrl, '_blank');
	}

	async function handleSendWhatsapp() {
		if (!selectedContact?.mobileNo) {
			toast.error('No contact number available.');
			return;
		}

		let blob: Blob | null = null;
		let mimeType = 'image/png';

		if (imageSourceType === 'local') {
			if (whatsappImage) {
				blob = whatsappImage;
				mimeType = whatsappImage.type;
			}
		} else if (selectedProductCode && whatsappImagePreview) {
			const prodMatch = crmProducts.find(p => p.code === selectedProductCode);
			let matchedImg: CrmWhatsappImage | undefined;

			if (prodMatch?.whatsappImageCode) {
				matchedImg = preSavedImages.find(img => 
					img.name?.toLowerCase() === prodMatch.whatsappImageCode?.toLowerCase() ||
					img.id === prodMatch.whatsappImageCode
				);
			}
			if (!matchedImg) {
				matchedImg = preSavedImages.find(img => img.products?.split(',').map(s => s.trim().toLowerCase()).includes(selectedProductCode.toLowerCase()));
			}

			if (matchedImg) {
				isCopyingWhatsapp = true;
				try {
					if (matchedImg.base64Data) {
						blob = base64ToBlob(matchedImg.base64Data);
						mimeType = blob.type;
					} else if (matchedImg.imageUrl) {
						blob = await fetchUrlAsBlob(matchedImg.imageUrl);
						mimeType = blob.type;
					}
				} catch (err: any) {
					console.error(err);
					toast.error('Failed to retrieve image data. Opening WhatsApp directly.');
					openWhatsappUrl();
					isCopyingWhatsapp = false;
					return;
				}
			}
		}

		if (!blob) {
			// Send text message directly if no image attached
			openWhatsappUrl();
			toast.success('Opened WhatsApp with message text!');
			return;
		}

		isCopyingWhatsapp = true;
		try {
			// 1. Copy image to clipboard
			if (mimeType !== 'image/png') {
				blob = await convertToPngBlob(blob);
			}

			await navigator.clipboard.write([
				new ClipboardItem({
					[blob.type]: blob
				})
			]);

			// 2. Open WhatsApp Web or app
			openWhatsappUrl();
			toast.success('Image copied to clipboard! Paste it (Ctrl+V) in WhatsApp.');
		} catch (err: any) {
			console.error(err);
			toast.error('Failed to copy image automatically. Opening WhatsApp, please paste the image manually.');
			openWhatsappUrl();
		} finally {
			isCopyingWhatsapp = false;
		}
	}
</script>

<!-- WhatsApp Product & Details Share -->
<div class="border border-border bg-muted/10 rounded-xl p-4 space-y-4">
	<div class="flex items-center justify-between">
		<div class="space-y-0.5">
			<h4 class="text-sm font-semibold">Share Product & Offer on WhatsApp</h4>
			<p class="text-xs text-muted-foreground">Select a CRM product to fetch regional pricing, image, and send customized WhatsApp offers.</p>
		</div>
		<Icon name="message-circle-more" class="size-6 text-emerald-500 shrink-0" />
	</div>

	<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
		<!-- Select Product Zone -->
		<div class="space-y-1.5 flex flex-col">
			<div class="flex items-center justify-between mb-0.5">
				<span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Select Product</span>
				<div class="flex border border-border rounded-lg overflow-hidden bg-muted/20 text-[10px] font-bold">
					<button
						type="button"
						class="px-2 py-0.5 transition-all {imageSourceType === 'product' ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'}"
						onclick={() => { imageSourceType = 'product'; clearWhatsappImage(); }}
					>
						Product
					</button>
					<button
						type="button"
						class="px-2 py-0.5 transition-all {imageSourceType === 'local' ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'}"
						onclick={() => { imageSourceType = 'local'; whatsappImagePreview = ''; }}
					>
						Local Upload
					</button>
				</div>
			</div>

			{#if imageSourceType === 'product'}
				<div class="space-y-2">
					{#if loadingWhatsappData}
						<div class="flex items-center gap-2 text-xs text-muted-foreground py-2">
							<Loader2 class="size-3 animate-spin" /> Loading products...
						</div>
					{:else}
						<Select
							options={productOptions}
							value={selectedProductCode}
							valueKey="value"
							labelKey="label"
							placeholder="Choose CRM Product..."
							class="rounded-xl h-10 w-full bg-card"
							onSelect={handleProductChange}
						/>
						{#if selectedProductCode}
							<div class="flex items-center gap-1.5 text-xs text-muted-foreground px-1">
								{#if fetchingPrice}
									<Loader2 class="size-3 animate-spin text-emerald-600" />
									<span>Fetching price...</span>
								{:else if fetchedPrice != null}
									<Icon name="tag" class="size-3.5 text-emerald-600" />
									<span class="font-semibold text-emerald-700 dark:text-emerald-400">Price: ₹{formatPrice(fetchedPrice)}</span>
									{#if selectedContact?.respCenter}
										<span class="text-[10px] text-muted-foreground">({selectedContact.respCenter})</span>
									{/if}
								{:else if selectedContact?.respCenter}
									<span class="text-[11px] text-amber-600">No price configured for RC: {selectedContact.respCenter}</span>
								{:else}
									<span class="text-[11px] text-muted-foreground">No contact responsibility center set</span>
								{/if}
							</div>
						{/if}

						{#if whatsappImagePreview}
							<div class="relative h-24 border border-border rounded-xl overflow-hidden bg-card flex items-center justify-center">
								<img
									src={whatsappImagePreview}
									alt="Product Preview"
									class="h-full w-full object-contain p-1"
								/>
							</div>
						{:else if selectedProductCode}
							<div class="text-[11px] text-muted-foreground bg-muted/20 border border-border/50 rounded-xl p-2 text-center">
								No linked image found for this product. You can select "Local Upload" to attach an image.
							</div>
						{/if}
					{/if}
				</div>
			{:else}
				{#if !whatsappImage}
					<label
						class="flex flex-col items-center justify-center h-28 border border-dashed border-border rounded-xl cursor-pointer hover:bg-muted/30 transition-colors"
					>
						<Icon name="upload" class="size-6 text-muted-foreground/60 mb-1" />
						<span class="text-xs text-muted-foreground font-medium">Click or Drag Image</span>
						<input
							type="file"
							accept="image/*"
							class="hidden"
							onchange={handleWhatsappImageChange}
						/>
					</label>
				{:else}
					<div class="relative h-28 border border-border rounded-xl overflow-hidden bg-card flex items-center justify-center group">
						<img
							src={whatsappImagePreview}
							alt="Preview"
							class="h-full w-full object-contain p-1"
						/>
						<button
							type="button"
							onclick={clearWhatsappImage}
							class="absolute top-2 right-2 p-1.5 bg-rose-500 hover:bg-rose-600 text-white rounded-lg opacity-90 transition-all shadow-md"
							title="Remove image"
						>
							<Icon name="trash" class="size-3.5" />
						</button>
					</div>
				{/if}
			{/if}
		</div>

		<!-- Message Caption & Template -->
		<div class="space-y-1.5 flex flex-col justify-between">
			<div class="space-y-2">
				<div class="space-y-1.5">
					<span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Select Message Template</span>
					<Select
						options={templateOptions}
						value={selectedTemplateId}
						valueKey="value"
						labelKey="label"
						placeholder="Choose message template..."
						class="rounded-xl h-10 w-full bg-card"
						onSelect={handleTemplateChange}
					/>
				</div>
				<div class="space-y-1.5">
					<span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">WhatsApp Message Text</span>
					<Input
						placeholder="Write message / caption here..."
						bind:value={whatsappCaption}
						class="rounded-xl h-10 bg-card"
					/>
				</div>
			</div>
			
			<Button
				onclick={handleSendWhatsapp}
				disabled={isCopyingWhatsapp}
				class="w-full h-10 gap-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl shadow-md font-semibold transition-all mt-2"
			>
				{#if isCopyingWhatsapp}
					<Loader2 class="size-4 animate-spin shrink-0" />
				{:else}
					<Icon name="send" class="size-4" />
				{/if}
				Send via WhatsApp
			</Button>
		</div>

	</div>
</div>
