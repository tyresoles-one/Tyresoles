<script lang="ts">
	import { untrack } from 'svelte';
	import { usePaginatedList } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import * as Dialog from '$lib/components/ui/dialog';
	import * as Field from '$lib/components/ui/field';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { TableCell, TableHead } from '$lib/components/ui/table';
	import { TableActions } from '$lib/components/venUI/tableActions';
	import MasterList from '$lib/components/venUI/masterList/MasterList.svelte';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import CrmSettingsView from './CrmSettingsView.svelte';

	import { graphqlQuery, graphqlMutation, buildMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	type CrmMasterItem = {
		id: number;
		name: string;
		parentId?: number | null;
		isPositive?: boolean;
	};

	type CrmMasterType =
		| 'CONTACT_TYPE'
		| 'CONTACT_CATEGORY'
		| 'SOURCE'
		| 'STAGE'
		| 'PRIORITY'
		| 'ACTIVITY_TYPE'
		| 'ACTIVITY_OUTCOME'
		| 'WHATSAPP_IMAGE'
		| 'WHATSAPP_TEMPLATE'
		| 'CRM_PRODUCTS'
		| 'ENTITY_TYPE'
		| 'APPLICATION'
		| 'VEHICLE_MAKE'
		| 'VEHICLE_MODEL'
		| 'VEHICLE_TYPE'
		| 'CRM_SETTINGS';

	type CrmProduct = {
		id: string;
		code: string;
		category?: string | null;
		productGroup?: string | null;
		finalPrice: number;
		respCenters?: string | null;
		createdAt: string;
	};

	type CrmMasterItemsResult = {
		crmMasterItems: CrmMasterItem[];
	};

	type CreateItemResult = {
		createCrmMasterItem: CrmMasterItem;
	};

	type UpdateItemResult = {
		updateCrmMasterItem: CrmMasterItem;
	};

	type DeleteItemResult = {
		deleteCrmMasterItem: boolean;
	};

	type CrmWhatsappImage = {
		id: string;
		name: string;
		imageUrl?: string | null;
		base64Data?: string | null;
		products?: string | null;
		createdAt: string;
	};

	type CrmWhatsappTemplate = {
		id: string;
		name: string;
		language: string;
		messageText: string;
		createdAt: string;
	};

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				name
				parentId
				isPositive
			}
		}
	` as unknown as TypedDocumentNode<CrmMasterItemsResult, { type: CrmMasterType; where?: any }>;

	const CreateCrmMasterItemDocument = buildMutation`
		mutation CreateCrmMasterItem($type: CrmMasterType!, $name: String!, $parentId: Int, $isPositive: Boolean) {
			createCrmMasterItem(type: $type, name: $name, parentId: $parentId, isPositive: $isPositive) {
				id
				name
				parentId
				isPositive
			}
		}
	` as unknown as TypedDocumentNode<CreateItemResult, { type: CrmMasterType; name: string; parentId?: number | null; isPositive?: boolean }>;

	const UpdateCrmMasterItemDocument = buildMutation`
		mutation UpdateCrmMasterItem($type: CrmMasterType!, $id: Int!, $name: String!, $parentId: Int, $isPositive: Boolean) {
			updateCrmMasterItem(type: $type, id: $id, name: $name, parentId: $parentId, isPositive: $isPositive) {
				id
				name
				parentId
				isPositive
			}
		}
	` as unknown as TypedDocumentNode<UpdateItemResult, { type: CrmMasterType; id: number; name: string; parentId?: number | null; isPositive?: boolean }>;

	const DeleteCrmMasterItemDocument = buildMutation`
		mutation DeleteCrmMasterItem($type: CrmMasterType!, $id: Int!) {
			deleteCrmMasterItem(type: $type, id: $id)
		}
	` as unknown as TypedDocumentNode<DeleteItemResult, { type: CrmMasterType; id: number }>;

	const GetCrmWhatsappImagesDocument = buildQuery`
		query GetCrmWhatsappImages {
			images: getCrmWhatsappImages {
				id
				name
				imageUrl
				base64Data
				products
				createdAt
			}
		}
	` as unknown as TypedDocumentNode<{ images: CrmWhatsappImage[] }, {}>;

	const SaveCrmWhatsappImageDocument = buildMutation`
		mutation SaveCrmWhatsappImage($input: CrmWhatsappImageInput!) {
			saveCrmWhatsappImage(input: $input) {
				id
				name
				imageUrl
				base64Data
				products
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmWhatsappImage: CrmWhatsappImage }, { input: any }>;

	const DeleteCrmWhatsappImageDocument = buildMutation`
		mutation DeleteCrmWhatsappImage($id: UUID!) {
			deleteCrmWhatsappImage(id: $id)
		}
	` as unknown as TypedDocumentNode<{ deleteCrmWhatsappImage: boolean }, { id: string }>;

	const GetCrmWhatsappTemplatesDocument = buildQuery`
		query GetCrmWhatsappTemplates {
			templates: getCrmWhatsappTemplates {
				id
				name
				language
				messageText
				createdAt
			}
		}
	` as unknown as TypedDocumentNode<{ templates: CrmWhatsappTemplate[] }, {}>;

	const GetCrmContactProductsDocument = buildQuery`
		query GetCrmContactProducts($respCenter: String) {
			getCrmContactProducts(respCenter: $respCenter)
		}
	` as unknown as TypedDocumentNode<{ getCrmContactProducts: string[] }, { respCenter?: string }>;
	const SaveCrmWhatsappTemplateDocument = buildMutation`
		mutation SaveCrmWhatsappTemplate($input: CrmWhatsappTemplateInput!) {
			saveCrmWhatsappTemplate(input: $input) {
				id
				name
				language
				messageText
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmWhatsappTemplate: CrmWhatsappTemplate }, { input: any }>;

	const DeleteCrmWhatsappTemplateDocument = buildMutation`
		mutation DeleteCrmWhatsappTemplate($id: UUID!) {
			deleteCrmWhatsappTemplate(id: $id)
		}
	` as unknown as TypedDocumentNode<{ deleteCrmWhatsappTemplate: boolean }, { id: string }>;

	const GetCrmProductsDocument = buildQuery`
		query GetCrmProducts($where: CrmProductFilterInput) {
			products: getCrmProducts(where: $where) {
				id
				code
				category
				productGroup
				finalPrice
				respCenters
				whatsappImageCode
				createdAt
			}
		}
	` as unknown as TypedDocumentNode<{ products: CrmProduct[] }, { where?: any }>;

	const SaveCrmProductDocument = buildMutation`
		mutation SaveCrmProduct($input: CrmProductInput!) {
			saveCrmProduct(input: $input) {
				id
				code
				category
				productGroup
				finalPrice
				respCenters
				whatsappImageCode
				createdAt
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmProduct: CrmProduct }, { input: any }>;

	const DeleteCrmProductDocument = buildMutation`
		mutation DeleteCrmProduct($id: UUID!) {
			deleteCrmProduct(id: $id)
		}
	` as unknown as TypedDocumentNode<{ deleteCrmProduct: boolean }, { id: string }>;

	const GetCrmSettingDocument = buildQuery`
		query GetCrmSetting($key: String!) {
			getCrmSetting(key: $key) {
				key
				value
			}
		}
	` as unknown as TypedDocumentNode<{ getCrmSetting: { key: string; value: string } | null }, { key: string }>;

	const GetCrmCustomerItemPriceDocument = buildQuery`
		query GetCrmCustomerItemPrice($itemNo: String!, $salesCode: String!) {
			price: getCrmCustomerItemPrice(itemNo: $itemNo, salesCode: $salesCode)
		}
	` as unknown as TypedDocumentNode<{ price: number | null }, { itemNo: string; salesCode: string }>;

	const SaveCrmSettingDocument = buildMutation`
		mutation SaveCrmSetting($key: String!, $value: String!, $description: String) {
			saveCrmSetting(key: $key, value: $value, description: $description) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmSetting: { success: boolean; message: string } }, { key: string; value: string; description?: string }>;

	const lookupTypes: { type: CrmMasterType; label: string; icon: string; description: string }[] = [
		{
			type: 'ENTITY_TYPE',
			label: 'Crm Entity Types',
			icon: 'building-2',
			description: 'Manage entity classifications like Fleet Operator or Dealer.'
		},
		{
			type: 'CONTACT_TYPE',
			label: 'Contact Types',
			icon: 'user-cog',
			description: 'Manage relationship categories for fleet contacts.'
		},
		{
			type: 'CONTACT_CATEGORY',
			label: 'Contact Categories',
			icon: 'tags',
			description: 'Categorize contacts (e.g. VIP, Regular).'
		},
		{
			type: 'SOURCE',
			label: 'Crm Sources',
			icon: 'share-2',
			description: 'Track how new retread leads hear about us.'
		},
		{
			type: 'STAGE',
			label: 'Crm Stages',
			icon: 'git-merge',
			description: 'Define pipeline stages for retreading opportunities.'
		},
		{
			type: 'PRIORITY',
			label: 'Crm Priorities',
			icon: 'alert-circle',
			description: 'Set urgency level for opportunities and deals.'
		},
		{
			type: 'ACTIVITY_TYPE',
			label: 'Activity Types',
			icon: 'phone-call',
			description: 'Sales interaction channels (e.g., Yard Audit).'
		},
		{
			type: 'ACTIVITY_OUTCOME',
			label: 'Activity Outcomes',
			icon: 'check-square',
			description: 'Log standard results of logged activities.'
		},
		{
			type: 'WHATSAPP_IMAGE',
			label: 'WhatsApp Images',
			icon: 'image',
			description: 'Manage pre-saved marketing images for WhatsApp.'
		},
		{
			type: 'WHATSAPP_TEMPLATE',
			label: 'WhatsApp Templates',
			icon: 'message-square',
			description: 'Manage multi-language message templates.'
		},
		{
			type: 'CRM_PRODUCTS',
			label: 'CRM Products',
			icon: 'package',
			description: 'Manage CRM products, final prices, and responsibility centers.'
		},
		{
			type: 'APPLICATION',
			label: 'Fleet Applications',
			icon: 'briefcase',
			description: 'Vehicle application categories (e.g. Long Haul).'
		},
		{
			type: 'VEHICLE_MAKE',
			label: 'Vehicle Makes',
			icon: 'truck',
			description: 'Vehicle manufacturers (e.g. Tata, Ashok Leyland).'
		},
		{
			type: 'VEHICLE_MODEL',
			label: 'Vehicle Models',
			icon: 'cog',
			description: 'Vehicle models under specific makes.'
		},
		{
			type: 'VEHICLE_TYPE',
			label: 'Vehicle Types',
			icon: 'car',
			description: 'Types of vehicles (e.g. Truck, Bus).'
		},
		{
			type: 'CRM_SETTINGS',
			label: 'CRM Settings',
			icon: 'settings',
			description: 'Configure global CRM settings and mappings.'
		}
	];

	let activeTab = $state<CrmMasterType>('CONTACT_TYPE');
	let viewMode = $state<'grid' | 'table'>('grid');
	let isSidebarExpanded = $state(true);

	const lookupList = usePaginatedList<CrmMasterItem>({
		query: GetCrmMasterItemsDocument,
		dataPath: 'crmMasterItems',
		itemsPath: 'crmMasterItems',
		countPath: 'crmMasterItems.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			type: activeTab === 'CRM_SETTINGS' ? 'CONTACT_TYPE' : activeTab,
			where: term ? { name: { contains: term } } : null
		}),
		serverVariableAllowlist: ['type', 'where']
	});

	const imagesList = usePaginatedList<CrmWhatsappImage>({
		query: GetCrmWhatsappImagesDocument,
		dataPath: 'images',
		itemsPath: 'images',
		countPath: 'images.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			where: term ? { name: { contains: term } } : null
		})
	});

	const templatesList = usePaginatedList<CrmWhatsappTemplate>({
		query: GetCrmWhatsappTemplatesDocument,
		dataPath: 'templates',
		itemsPath: 'templates',
		countPath: 'templates.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			where: term ? { name: { contains: term } } : null
		})
	});

	const productsList = usePaginatedList<CrmProduct>({
		query: GetCrmProductsDocument,
		dataPath: 'products',
		itemsPath: 'products',
		countPath: 'products.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			where: term ? { code: { contains: term } } : null
		})
	});

	const currentList = $derived.by(() => {
		if (activeTab === 'WHATSAPP_IMAGE') return imagesList;
		if (activeTab === 'WHATSAPP_TEMPLATE') return templatesList;
		if (activeTab === 'CRM_PRODUCTS') return productsList;
		return lookupList;
	});

	let activityTypes = $state<CrmMasterItem[]>([]);
	let vehicleTypes = $state<CrmMasterItem[]>([]);
	let vehicleMakes = $state<CrmMasterItem[]>([]);

	async function loadActivityTypes() {
		const res = await graphqlMutation<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
			variables: { type: 'ACTIVITY_TYPE' }
		});
		if (res.success && res.data) {
			activityTypes = res.data.crmMasterItems;
		}
	}

	async function loadVehicleTypes() {
		const res = await graphqlMutation<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
			variables: { type: 'VEHICLE_TYPE' }
		});
		if (res.success && res.data) {
			vehicleTypes = res.data.crmMasterItems;
		}
	}

	async function loadVehicleMakes() {
		const res = await graphqlMutation<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
			variables: { type: 'VEHICLE_MAKE' }
		});
		if (res.success && res.data) {
			vehicleMakes = res.data.crmMasterItems;
		}
	}

	let dummyForm = $state({
		values: { products: '' as string },
		setTouched: (name: string) => {},
		errors: {}
	});

	// Reactively refresh items whenever the active type changes
	$effect(() => {
		const tab = activeTab;
		untrack(() => {
			if (tab === 'ACTIVITY_OUTCOME') {
				loadActivityTypes();
			} else if (tab === 'VEHICLE_MAKE') {
				loadVehicleTypes();
			} else if (tab === 'VEHICLE_MODEL') {
				loadVehicleMakes();
			}

			if (tab === 'WHATSAPP_IMAGE') {
				imagesList.onRefresh();
			} else if (tab === 'WHATSAPP_TEMPLATE') {
				templatesList.onRefresh();
			} else if (tab === 'CRM_PRODUCTS') {
				productsList.onRefresh();
			} else if (tab === 'CRM_SETTINGS') {
				// Do not fetch lookupList for CRM Settings
			} else {
				lookupList.pagination.setVariables({
					type: tab,
					where: lookupList.searchQuery.value ? { name: { contains: lookupList.searchQuery.value } } : null
				});
				lookupList.onRefresh();
			}
		});
	});

	// Dialog editing states
	let dialogOpen = $state(false);
	let dialogMode = $state<'add' | 'edit'>('add');
	let editItemId = $state<number | null>(null);
	let editItemGuid = $state<string | null>(null);
	let itemNameInput = $state('');
	let itemParentId = $state<number | null>(null);
	let itemIsPositive = $state(false);
	let isSaving = $state(false);

	// Image fields
	let imageInputUrl = $state('');
	let imageInputBase64 = $state('');
	let imageLocalFile = $state<File | null>(null);
	let imageLocalPreview = $state('');

	// Template fields
	let templateLanguage = $state('English');
	let templateMessageText = $state('');

	// Product fields
	let productFormValues = $state({
		code: '',
		category: '',
		productGroup: '',
		finalPrice: 0,
		respCenters: ''
	});

	let productForm = $state({
		get values() {
			return productFormValues as unknown as Record<string, unknown>;
		},
		set values(v: Record<string, unknown>) {
			productFormValues = v as any;
		},
		setTouched: (_name: string) => {},
		errors: {}
	});

	async function fetchAndPrefillPrice(itemNo: string, respCentersStr?: string) {
		if (!itemNo) return;
		try {
			let salesCode = '';
			const settingRes = await graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(GetCrmSettingDocument, {
				variables: { key: 'CUSTOMER_PRICE_GROUP_MAPPING' }
			});
			if (settingRes.success && settingRes.data?.getCrmSetting?.value) {
				const mappings: { respCenters: string[]; priceGroupCode: string }[] = JSON.parse(settingRes.data.getCrmSetting.value);
				if (respCentersStr) {
					const rcList = respCentersStr.split(',').map(r => r.trim().toLowerCase()).filter(Boolean);
					const match = mappings.find(m => m.respCenters?.some(rc => rcList.includes(rc.trim().toLowerCase())));
					if (match) salesCode = match.priceGroupCode;
				}
				if (!salesCode && mappings.length > 0) {
					salesCode = mappings[0].priceGroupCode;
				}
			}

			if (!salesCode) return;

			const priceRes = await graphqlQuery<{ price: number | null }>(GetCrmCustomerItemPriceDocument, {
				variables: { itemNo, salesCode }
			});

			if (priceRes.success && priceRes.data?.price != null) {
				productFormValues.finalPrice = priceRes.data.price;
			}
		} catch (e) {
			console.error('Failed to prefill price', e);
		}
	}

	// Dialog deletion states
	let deleteDialogOpen = $state(false);
	let deleteItemId = $state<number | null>(null);
	let deleteItemGuid = $state<string | null>(null);
	let deleteItemName = $state('');
	let isDeleting = $state(false);

	const activeConfig = $derived(lookupTypes.find((x) => x.type === activeTab)!);

	function openAddDialog() {
		dialogMode = 'add';
		editItemId = null;
		editItemGuid = null;
		itemNameInput = '';
		itemParentId = null;
		itemIsPositive = false;

		imageInputUrl = '';
		imageInputBase64 = '';
		imageLocalFile = null;
		imageLocalPreview = '';
		dummyForm.values.products = '';

		templateLanguage = 'English';
		templateMessageText = '';

		productFormValues = {
			code: '',
			category: '',
			productGroup: '',
			finalPrice: 0,
			respCenters: '',
			whatsappImageCode: ''
		};

		dialogOpen = true;
	}

	function openEditDialog(item: any) {
		dialogMode = 'edit';
		itemNameInput = item.name || item.code || '';

		if (activeTab === 'WHATSAPP_IMAGE') {
			editItemGuid = item.id;
			imageInputUrl = item.imageUrl || '';
			imageInputBase64 = item.base64Data || '';
			imageLocalFile = null;
			imageLocalPreview = item.base64Data || item.imageUrl || '';
			dummyForm.values.products = item.products || '';
		} else if (activeTab === 'WHATSAPP_TEMPLATE') {
			editItemGuid = item.id;
			templateLanguage = item.language || 'English';
			templateMessageText = item.messageText || '';
		} else if (activeTab === 'CRM_PRODUCTS') {
			editItemGuid = item.id;
			itemNameInput = item.code || '';
			productFormValues = {
				code: item.code || '',
				category: item.category || '',
				productGroup: item.productGroup || '',
				finalPrice: item.finalPrice || 0,
				respCenters: item.respCenters || '',
				whatsappImageCode: item.whatsappImageCode || ''
			};
		} else {
			editItemId = item.id;
			itemParentId = item.parentId ?? null;
			itemIsPositive = item.isPositive ?? false;
		}

		dialogOpen = true;
	}

	function openDeleteDialog(item: any) {
		deleteItemName = item.name || item.code || '';
		if (activeTab === 'WHATSAPP_IMAGE' || activeTab === 'WHATSAPP_TEMPLATE' || activeTab === 'CRM_PRODUCTS') {
			deleteItemGuid = item.id;
			deleteItemId = null;
		} else {
			deleteItemId = item.id;
			deleteItemGuid = null;
		}
		deleteDialogOpen = true;
	}

	function handleImageUpload(e: Event) {
		const target = e.target as HTMLInputElement;
		const file = target.files?.[0];
		if (file) {
			if (!file.type.startsWith('image/')) {
				toast.error('Please select an image file.');
				return;
			}
			imageLocalFile = file;
			
			const reader = new FileReader();
			reader.onload = () => {
				const base64 = reader.result as string;
				imageInputBase64 = base64;
				imageLocalPreview = base64;
			};
			reader.readAsDataURL(file);
		}
	}

	function clearUploadedImage() {
		imageLocalFile = null;
		imageInputBase64 = '';
		imageLocalPreview = '';
	}

	async function saveItem() {
		const name = itemNameInput.trim();
		if (activeTab !== 'CRM_PRODUCTS' && !name) {
			toast.error('Item name cannot be empty');
			return;
		}

		isSaving = true;
		try {
			if (activeTab === 'WHATSAPP_IMAGE') {
				const input: any = {
					id: editItemGuid || null,
					name,
					imageUrl: imageInputUrl.trim() || null,
					base64Data: imageInputBase64.trim() || null,
					products: dummyForm.values.products?.replace(/,\s+/g, ',') || null
				};

				const res = await graphqlMutation<any>(SaveCrmWhatsappImageDocument, {
					variables: { input }
				});

				if (res.success && res.data?.saveCrmWhatsappImage) {
					toast.success(`Image "${name}" saved successfully.`);
					dialogOpen = false;
					imagesList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to save image.');
				}
			} else if (activeTab === 'WHATSAPP_TEMPLATE') {
				const input: any = {
					id: editItemGuid || null,
					name,
					language: templateLanguage.trim() || 'English',
					messageText: templateMessageText.trim()
				};

				if (!input.messageText) {
					toast.error('Message text cannot be empty');
					isSaving = false;
					return;
				}

				const res = await graphqlMutation<any>(SaveCrmWhatsappTemplateDocument, {
					variables: { input }
				});

				if (res.success && res.data?.saveCrmWhatsappTemplate) {
					toast.success(`Template "${name}" saved successfully.`);
					dialogOpen = false;
					templatesList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to save template.');
				}
			} else if (activeTab === 'CRM_PRODUCTS') {
				const code = (productFormValues.code || name).trim();
				if (!code) {
					toast.error('Product Code cannot be empty');
					isSaving = false;
					return;
				}

				const input: any = {
					id: editItemGuid || null,
					code,
					category: productFormValues.category?.trim() || null,
					productGroup: productFormValues.productGroup?.trim() || null,
					finalPrice: Number(productFormValues.finalPrice) || 0,
					respCenters: productFormValues.respCenters?.replace(/,\s+/g, ',') || null,
					whatsappImageCode: productFormValues.whatsappImageCode?.trim() || null
				};

				const res = await graphqlMutation<any>(SaveCrmProductDocument, {
					variables: { input }
				});

				if (res.success && res.data?.saveCrmProduct) {
					toast.success(`Product "${code}" saved successfully.`);
					dialogOpen = false;
					productsList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to save product.');
				}
			} else {
				if (dialogMode === 'add') {
					const res = await graphqlMutation<CreateItemResult>(CreateCrmMasterItemDocument, {
						variables: { type: activeTab, name, parentId: itemParentId, isPositive: itemIsPositive }
					});

					if (res.success && res.data?.createCrmMasterItem) {
						toast.success(`"${name}" added successfully.`);
						dialogOpen = false;
						lookupList.onRefresh();
					} else {
						toast.error(res.error || 'Failed to add item');
					}
				} else {
					if (editItemId === null) return;
					const res = await graphqlMutation<UpdateItemResult>(UpdateCrmMasterItemDocument, {
						variables: { type: activeTab, id: editItemId, name, parentId: itemParentId, isPositive: itemIsPositive }
					});

					if (res.success && res.data?.updateCrmMasterItem) {
						toast.success(`Item updated to "${name}".`);
						dialogOpen = false;
						lookupList.onRefresh();
					} else {
						toast.error(res.error || 'Failed to update item');
					}
				}
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while saving.');
		} finally {
			isSaving = false;
		}
	}

	async function confirmDelete() {
		isDeleting = true;
		try {
			if (activeTab === 'WHATSAPP_IMAGE') {
				if (deleteItemGuid === null) return;
				const res = await graphqlMutation<any>(DeleteCrmWhatsappImageDocument, {
					variables: { id: deleteItemGuid }
				});

				if (res.success && res.data?.deleteCrmWhatsappImage) {
					toast.success(`"${deleteItemName}" deleted successfully.`);
					deleteDialogOpen = false;
					imagesList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to delete image');
				}
			} else if (activeTab === 'WHATSAPP_TEMPLATE') {
				if (deleteItemGuid === null) return;
				const res = await graphqlMutation<any>(DeleteCrmWhatsappTemplateDocument, {
					variables: { id: deleteItemGuid }
				});

				if (res.success && res.data?.deleteCrmWhatsappTemplate) {
					toast.success(`"${deleteItemName}" deleted successfully.`);
					deleteDialogOpen = false;
					templatesList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to delete template');
				}
			} else if (activeTab === 'CRM_PRODUCTS') {
				if (deleteItemGuid === null) return;
				const res = await graphqlMutation<any>(DeleteCrmProductDocument, {
					variables: { id: deleteItemGuid }
				});

				if (res.success && res.data?.deleteCrmProduct) {
					toast.success(`"${deleteItemName}" deleted successfully.`);
					deleteDialogOpen = false;
					productsList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to delete product');
				}
			} else {
				if (deleteItemId === null) return;
				const res = await graphqlMutation<DeleteItemResult>(DeleteCrmMasterItemDocument, {
					variables: { type: activeTab, id: deleteItemId }
				});

				if (res.success && res.data?.deleteCrmMasterItem) {
					toast.success(`"${deleteItemName}" deleted successfully.`);
					deleteDialogOpen = false;
					lookupList.onRefresh();
				} else {
					toast.error(res.error || 'Failed to delete item');
				}
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while deleting.');
		} finally {
			isDeleting = false;
		}
	}
</script>

<svelte:head>
	<title>{activeConfig.label} CRM Master | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background text-foreground pb-20 selection:bg-primary/20">
	<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-8 relative z-10">
		<div class="flex flex-col lg:flex-row gap-8">
			<!-- Lookup selection sidebar (desktop) / scrolling pill selector (mobile) -->
			<aside class="w-full shrink-0 transition-all duration-300 ease-in-out {isSidebarExpanded ? 'lg:w-80' : 'lg:w-[72px]'}">
				<!-- Scrolling horizontal tabs on mobile, vertical list on desktop -->
				<div class="lg:sticky lg:top-24 max-h-[calc(100vh-8rem)] overflow-y-auto pr-1 scrollbar-hide">
					<div class="flex flex-col gap-1">
						<div class="flex items-center justify-between px-3 mb-2 hidden lg:flex">
							{#if isSidebarExpanded}
								<h2 class="text-xs font-semibold text-muted-foreground uppercase tracking-wider transition-opacity duration-300">
									CRM Masters
								</h2>
							{/if}
							<button 
								class="p-1.5 rounded-md hover:bg-muted text-muted-foreground transition-colors {isSidebarExpanded ? '' : 'mx-auto'}" 
								onclick={() => isSidebarExpanded = !isSidebarExpanded}
								title={isSidebarExpanded ? 'Collapse Sidebar' : 'Expand Sidebar'}
							>
								<Icon name={isSidebarExpanded ? 'panel-left-close' : 'panel-left-open'} class="size-4" />
							</button>
						</div>

						<!-- Mobile horizontal scroll container -->
						<div class="flex flex-row overflow-x-auto gap-2 pb-2 lg:pb-0 lg:flex-col lg:overflow-x-visible scrollbar-hide">
							{#each lookupTypes as item}
								{@const isActive = activeTab === item.type}
								{@const activeThemeClass = item.type === 'WHATSAPP_TEMPLATE'
									? 'bg-emerald-50 border-emerald-100 text-emerald-600 dark:bg-emerald-950/40 dark:border-emerald-900/30 dark:text-emerald-400'
									: item.type === 'WHATSAPP_IMAGE'
										? 'bg-blue-50 border-blue-100 text-blue-600 dark:bg-blue-950/40 dark:border-blue-900/30 dark:text-blue-400'
										: 'bg-indigo-50 border-indigo-100 text-indigo-600 dark:bg-indigo-950/40 dark:border-indigo-900/30 dark:text-indigo-400'}
								{@const activeIconBg = item.type === 'WHATSAPP_TEMPLATE'
									? 'bg-emerald-100/80 text-emerald-600 dark:bg-emerald-900/50 dark:text-emerald-400'
									: item.type === 'WHATSAPP_IMAGE'
										? 'bg-blue-100/80 text-blue-600 dark:bg-blue-900/50 dark:text-blue-400'
										: 'bg-indigo-100/80 text-indigo-600 dark:bg-indigo-900/50 dark:text-indigo-400'}

								<button
									type="button"
									title={!isSidebarExpanded ? item.label : undefined}
									onclick={() => (activeTab = item.type)}
									class="flex items-center gap-3 rounded-xl border transition-all text-left shrink-0 lg:shrink select-none group
										{isSidebarExpanded ? 'px-4 py-3' : 'p-3 lg:justify-center'} 
										{isActive
											? `${activeThemeClass} font-semibold shadow-xs`
											: 'border-border bg-card text-muted-foreground hover:text-foreground hover:bg-muted/50'}"
								>
									<div class="p-1.5 rounded-lg transition-colors duration-300 {isActive ? activeIconBg : 'bg-muted text-muted-foreground group-hover:bg-muted/80'}">
										<Icon name={item.icon} class="size-4" />
									</div>
									{#if isSidebarExpanded}
										<div class="min-w-0 transition-opacity duration-300">
											<div class="text-sm truncate">{item.label}</div>
											<div class="text-[10px] text-muted-foreground truncate hidden lg:block mt-0.5">{item.description}</div>
										</div>
									{:else}
										<div class="lg:hidden min-w-0">
											<div class="text-sm truncate">{item.label}</div>
										</div>
									{/if}
								</button>
							{/each}
						</div>
					</div>
				</div>
			</aside>

			<!-- Master List Content Area -->
			<main class="flex-1 min-w-0">
				{#if activeTab === 'CRM_SETTINGS'}
					<CrmSettingsView />
				{:else}
				<MasterList
					embedded={true}
					title={activeConfig.label}
					description={activeConfig.description}
					items={currentList.items}
					totalCount={currentList.totalCount}
					bind:searchQuery={currentList.searchQuery.value}
					bind:viewMode
					loading={currentList.loading}
					loadingMore={currentList.loadingMore}
					error={currentList.error}
					hasMore={currentList.hasMore}
					onLoadMore={currentList.onLoadMore}
					onRefresh={currentList.onRefresh}
				>
					{#snippet actions()}
						<Button
							size="sm"
							class="gap-2 shrink-0 bg-indigo-600 hover:bg-indigo-500 text-white font-medium shadow-lg hover:shadow-indigo-500/20 rounded-xl px-4 py-2 transition-all"
							onclick={openAddDialog}
						>
							<Icon name="plus" class="size-3.5" />
							<span>Add {activeConfig.label.endsWith('s') ? activeConfig.label.slice(0, -1) : activeConfig.label}</span>
						</Button>
					{/snippet}

					{#snippet gridItem(item: any)}
						{@const isTemplate = activeTab === 'WHATSAPP_TEMPLATE'}
						{@const isImage = activeTab === 'WHATSAPP_IMAGE'}
						{@const isProduct = activeTab === 'CRM_PRODUCTS'}
						{@const activeIconClass = isTemplate 
							? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-600 dark:text-emerald-400' 
							: isImage
								? 'bg-blue-500/10 border-blue-500/20 text-blue-600 dark:text-blue-400'
								: isProduct
									? 'bg-indigo-500/10 border-indigo-500/20 text-indigo-600 dark:text-indigo-400'
									: 'bg-indigo-500/10 border-indigo-500/20 text-indigo-600 dark:text-indigo-400'}
						{@const cardThemeClass = isTemplate
							? 'hover:border-emerald-500/30 dark:hover:border-emerald-500/20 hover:shadow-emerald-500/5'
							: isImage
								? 'hover:border-blue-500/30 dark:hover:border-blue-500/20 hover:shadow-blue-500/5'
								: isProduct
									? 'hover:border-indigo-500/30 dark:hover:border-indigo-500/20 hover:shadow-indigo-500/5'
									: 'hover:border-indigo-500/30 dark:hover:border-indigo-500/20 hover:shadow-indigo-500/5'}

						<div class="h-full rounded-xl border border-border bg-card hover:bg-accent/10 backdrop-blur-xs p-4 relative group flex flex-col justify-between transition-all duration-300 hover:shadow-md {cardThemeClass}">
							<div class="flex flex-col gap-3 h-full justify-between">
								<div class="space-y-3">
									{#if isImage}
										<div class="aspect-video w-full rounded-xl bg-slate-50 dark:bg-zinc-900/50 flex items-center justify-center overflow-hidden border border-border/80 shadow-xs relative group-hover:border-blue-500/20 transition-all duration-300">
											{#if item.base64Data || item.imageUrl}
												<!-- Blurred background mesh for premium styling -->
												<img src={item.base64Data || item.imageUrl} alt="" class="absolute inset-0 h-full w-full object-cover blur-md opacity-25 dark:opacity-15 scale-110 pointer-events-none" />
												<!-- Main image centered and uncropped -->
												<img src={item.base64Data || item.imageUrl} alt={item.name} class="h-full w-full object-contain relative z-10 transition-transform duration-500 group-hover:scale-105 p-1" />
											{:else}
												<div class="flex flex-col items-center gap-1 text-muted-foreground/40">
													<Icon name="image" class="size-8" />
													<span class="text-[10px]">No image uploaded</span>
												</div>
											{/if}
										</div>
									{/if}
									
									{#if isProduct}
										<div class="flex items-center justify-between gap-2 w-full">
											<div class="p-2 rounded-xl border transition-colors {activeIconClass}">
												<Icon name="package" class="size-4" />
											</div>
											<div class="bg-emerald-500/10 border border-emerald-500/20 text-emerald-700 dark:text-emerald-300 rounded-lg px-2.5 py-1 text-right shrink-0">
												<span class="text-[9px] font-semibold uppercase tracking-wider block text-emerald-600 dark:text-emerald-400 leading-none">Price</span>
												<span class="text-xs font-bold font-mono">₹{item.finalPrice?.toLocaleString('en-IN')}</span>
											</div>
										</div>

										<div class="space-y-1.5 mt-1">
											<h3 class="font-semibold text-xs text-foreground group-hover:text-primary transition-colors line-clamp-2 leading-snug">
												{item.code}
											</h3>

											<div class="flex flex-wrap items-center gap-1 pt-0.5">
												{#if item.category}
													<span class="inline-flex items-center px-1.5 py-0.5 rounded-md text-[10px] font-medium bg-slate-100 dark:bg-zinc-800 text-foreground/80">
														{item.category}
													</span>
												{/if}
												{#if item.productGroup}
													<span class="inline-flex items-center px-1.5 py-0.5 rounded-md text-[10px] font-medium bg-slate-100 dark:bg-zinc-800 text-muted-foreground">
														{item.productGroup}
													</span>
												{/if}
												{#if item.whatsappImageCode}
													<span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[10px] font-medium bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">
														<Icon name="image" class="size-2.5" />
														{item.whatsappImageCode}
													</span>
												{/if}
											</div>

											{#if item.respCenters}
												<div class="text-[10px] text-muted-foreground flex items-center gap-1 pt-1">
													<Icon name="building-2" class="size-3 text-muted-foreground/60 shrink-0" />
													<span class="truncate">RCs: {item.respCenters}</span>
												</div>
											{/if}
										</div>
									{:else}
										<div class="flex items-start justify-between gap-4">
											<div class="flex items-center gap-3">
												{#if !isImage}
													<div class="p-2.5 rounded-xl border transition-colors {activeIconClass}">
														<Icon name={activeConfig.icon} class="size-5" />
													</div>
												{/if}
												<div class="min-w-0">
													<h3 class="font-semibold text-sm text-foreground group-hover:text-primary transition-colors truncate">
														{item.name || item.code}
													</h3>
													{#if isTemplate && item.language && item.name.toLowerCase() !== item.language.toLowerCase()}
														<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[10px] font-medium bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 mt-1 animate-in fade-in duration-300">
															{item.language}
														</span>
													{/if}
													{#if isImage && item.products}
														<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[10px] font-medium bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400 mt-1 animate-in fade-in duration-300">
															<Icon name="package" class="size-2.5" />
															{item.products}
														</span>
													{/if}
													{#if !isTemplate && !isImage}
														<span class="text-[10px] font-mono text-muted-foreground mt-0.5 block truncate max-w-[180px]">
															ID: {item.id}
														</span>
														{#if activeTab === 'ACTIVITY_OUTCOME' || activeTab === 'VEHICLE_MAKE' || activeTab === 'VEHICLE_MODEL'}
															<div class="flex flex-col gap-1 mt-1">
																{#if item.parentId}
																	<span class="text-[10px] text-muted-foreground flex items-center gap-1">
																		<Icon name="git-branch" class="size-3" />
																		{#if activeTab === 'ACTIVITY_OUTCOME'}
																			{activityTypes.find(x => x.id === item.parentId)?.name || 'Unknown Type'}
																		{:else if activeTab === 'VEHICLE_MAKE'}
																			{vehicleTypes.find(x => x.id === item.parentId)?.name || 'Unknown Type'}
																		{:else if activeTab === 'VEHICLE_MODEL'}
																			{vehicleMakes.find(x => x.id === item.parentId)?.name || 'Unknown Make'}
																		{/if}
																	</span>
																{/if}
																{#if activeTab === 'ACTIVITY_OUTCOME' && item.isPositive}
																	<span class="inline-flex w-fit items-center gap-1 px-1.5 py-0.5 rounded-md text-[9px] font-medium bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">
																		<Icon name="check-circle" class="size-2.5" />
																		Positive
																	</span>
																{/if}
															</div>
														{/if}
													{/if}
												</div>
											</div>
										</div>
									{/if}

									{#if isTemplate}
										<!-- WhatsApp Chat Bubble Preview -->
										<div class="relative mt-2">
											<div class="bg-emerald-50/60 dark:bg-emerald-950/20 border border-emerald-100/50 dark:border-emerald-900/30 rounded-2xl rounded-tr-none p-3.5 text-xs text-foreground/90 whitespace-pre-wrap break-words leading-relaxed shadow-2xs relative">
												<p class="font-normal select-text">{item.messageText}</p>
												<div class="flex justify-end items-center gap-1 mt-2 text-[9px] text-muted-foreground/60 select-none">
													<span>
														{#if item.createdAt}
															{new Date(item.createdAt).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true })}
														{:else}
															{new Date().toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true })}
														{/if}
													</span>
													<span class="text-emerald-500">✓✓</span>
												</div>
											</div>
										</div>
									{/if}
								</div>

								<div class="flex items-center justify-between mt-2 pt-2 border-t border-border/40">
									<span class="text-[10px] text-muted-foreground">
										{#if item.createdAt}
											Added {new Date(item.createdAt).toLocaleDateString('en-IN')}
										{:else}
											Lookup config
										{/if}
									</span>

									<TableActions
										title={item.name || item.code}
										actions={[
											{
												label: 'Edit',
												icon: 'pencil',
												onClick: () => openEditDialog(item)
											},
											{
												label: 'Delete',
												icon: 'trash',
												onClick: () => openDeleteDialog(item),
												variant: 'destructive'
											}
										]}
									/>
								</div>
							</div>
						</div>
					{/snippet}

					{#snippet tableHeader()}
						{#if activeTab === 'WHATSAPP_IMAGE'}
							<TableHead class="w-[80px] text-center text-muted-foreground">Preview</TableHead>
							<TableHead class="text-muted-foreground">Name</TableHead>
							<TableHead class="text-muted-foreground">Source</TableHead>
							<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
						{:else}
							<TableHead class="w-[80px] text-center text-muted-foreground">ID</TableHead>
							<TableHead class="text-muted-foreground">Name</TableHead>
							{#if activeTab === 'ACTIVITY_OUTCOME'}
								<TableHead class="text-muted-foreground">Parent Type</TableHead>
								<TableHead class="text-muted-foreground">Positive</TableHead>
							{/if}
							{#if activeTab === 'VEHICLE_MAKE'}
								<TableHead class="text-muted-foreground">Parent Type</TableHead>
							{/if}
							{#if activeTab === 'VEHICLE_MODEL'}
								<TableHead class="text-muted-foreground">Parent Make</TableHead>
							{/if}
							{#if activeTab === 'WHATSAPP_TEMPLATE'}
								<TableHead class="text-muted-foreground">Language</TableHead>
								<TableHead class="text-muted-foreground">Template Text</TableHead>
							{/if}
							{#if activeTab === 'CRM_PRODUCTS'}
								<TableHead class="text-muted-foreground">Product Code</TableHead>
								<TableHead class="text-muted-foreground">Category</TableHead>
								<TableHead class="text-muted-foreground">Group</TableHead>
								<TableHead class="text-muted-foreground">Final Price</TableHead>
								<TableHead class="text-muted-foreground">Resp Centers</TableHead>
							{/if}
							<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
						{/if}
					{/snippet}

					{#snippet tableRow(item: any)}
						{#if activeTab === 'WHATSAPP_IMAGE'}
							<TableCell class="text-center font-mono text-xs text-muted-foreground p-3">
								<div class="size-10 rounded-lg bg-muted flex items-center justify-center overflow-hidden border border-border mx-auto font-medium">
									{#if item.base64Data || item.imageUrl}
										<img src={item.base64Data || item.imageUrl} alt={item.name} class="h-full w-full object-contain" />
									{:else}
										<Icon name="image" class="size-4 text-muted-foreground/40" />
									{/if}
								</div>
							</TableCell>
							<TableCell class="font-medium text-foreground">{item.name}</TableCell>
							<TableCell class="text-xs text-muted-foreground font-mono">
								{item.base64Data ? 'Uploaded Base64' : (item.imageUrl ? 'External URL' : 'None')}
							</TableCell>
							<TableCell class="text-right p-3">
								<TableActions
									title={item.name}
									actions={[
										{
											label: 'Edit',
											icon: 'edit',
											onClick: () => openEditDialog(item)
										},
										{
											label: 'Delete',
											icon: 'trash',
											onClick: () => openDeleteDialog(item),
											variant: 'destructive'
										}
									]}
								/>
							</TableCell>
						{:else if activeTab === 'CRM_PRODUCTS'}
							<TableCell class="font-medium text-foreground">{item.code}</TableCell>
							<TableCell class="text-xs text-muted-foreground">{item.category || '-'}</TableCell>
							<TableCell class="text-xs text-muted-foreground">{item.productGroup || '-'}</TableCell>
							<TableCell class="font-semibold text-xs text-emerald-600 dark:text-emerald-400">₹{item.finalPrice?.toLocaleString('en-IN')}</TableCell>
							<TableCell class="text-xs text-muted-foreground">{item.respCenters || 'All'}</TableCell>
							<TableCell class="text-right p-3">
								<TableActions
									title={item.code}
									actions={[
										{
											label: 'Edit',
											icon: 'edit',
											onClick: () => openEditDialog(item)
										},
										{
											label: 'Delete',
											icon: 'trash',
											onClick: () => openDeleteDialog(item),
											variant: 'destructive'
										}
									]}
								/>
							</TableCell>
						{:else}
							<TableCell class="text-center font-mono text-xs text-muted-foreground">{item.id}</TableCell>
							<TableCell class="font-medium text-foreground">{item.name}</TableCell>
							{#if activeTab === 'ACTIVITY_OUTCOME'}
								<TableCell class="text-xs text-muted-foreground">
									{activityTypes.find(x => x.id === item.parentId)?.name || '-'}
								</TableCell>
								<TableCell class="text-xs">
									{#if item.isPositive}
										<span class="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400">
											<Icon name="check-circle" class="size-3" /> Yes
										</span>
									{:else}
										<span class="text-muted-foreground">-</span>
									{/if}
								</TableCell>
							{/if}
							{#if activeTab === 'VEHICLE_MAKE'}
								<TableCell class="text-xs text-muted-foreground">
									{vehicleTypes.find(x => x.id === item.parentId)?.name || '-'}
								</TableCell>
							{/if}
							{#if activeTab === 'VEHICLE_MODEL'}
								<TableCell class="text-xs text-muted-foreground">
									{vehicleMakes.find(x => x.id === item.parentId)?.name || '-'}
								</TableCell>
							{/if}
							{#if activeTab === 'WHATSAPP_TEMPLATE'}
								<TableCell class="font-semibold text-xs text-indigo-600 dark:text-indigo-400">{item.language}</TableCell>
								<TableCell class="text-xs text-muted-foreground max-w-xs truncate">{item.messageText}</TableCell>
							{/if}
							<TableCell class="text-right p-3">
								<TableActions
									title={item.name}
									actions={[
										{
											label: 'Edit',
											icon: 'edit',
											onClick: () => openEditDialog(item)
										},
										{
											label: 'Delete',
											icon: 'trash',
											onClick: () => openDeleteDialog(item),
											variant: 'destructive'
										}
									]}
								/>
							</TableCell>
						{/if}
					{/snippet}
				</MasterList>
				{/if}
			</main>
		</div>
	</div>
</div>

<!-- Add/Edit Modal -->
<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>{dialogMode === 'add' ? 'Add' : 'Edit'} {activeConfig.label.endsWith('s') ? activeConfig.label.slice(0, -1) : activeConfig.label}</Dialog.Title>
		</Dialog.Header>

		<div class="flex flex-col gap-4 py-3">
			{#if activeTab !== 'CRM_PRODUCTS'}
				<Field.Field class="w-full">
					<Field.Label for="master-item-name">Name</Field.Label>
					<Field.Content>
						<Input
							id="master-item-name"
							bind:value={itemNameInput}
							placeholder={`e.g., New ${activeConfig.label.endsWith('s') ? activeConfig.label.slice(0, -1) : activeConfig.label}`}
							autocomplete="off"
							class="rounded-xl"
						/>
					</Field.Content>
				</Field.Field>
			{/if}

			{#if activeTab === 'ACTIVITY_OUTCOME' || activeTab === 'VEHICLE_MAKE' || activeTab === 'VEHICLE_MODEL'}
				<Field.Field class="w-full">
					<Field.Label for="master-item-parent">Parent 
						{#if activeTab === 'ACTIVITY_OUTCOME'}Activity Type{:else if activeTab === 'VEHICLE_MAKE'}Vehicle Type{:else if activeTab === 'VEHICLE_MODEL'}Vehicle Make{/if}
					</Field.Label>
					<Field.Content>
						<select
							id="master-item-parent"
							bind:value={itemParentId}
							class="flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
						>
							<option value={null}>-- Select Parent --</option>
							{#if activeTab === 'ACTIVITY_OUTCOME'}
								{#each activityTypes as pItem}
									<option value={pItem.id}>{pItem.name}</option>
								{/each}
							{:else if activeTab === 'VEHICLE_MAKE'}
								{#each vehicleTypes as pItem}
									<option value={pItem.id}>{pItem.name}</option>
								{/each}
							{:else if activeTab === 'VEHICLE_MODEL'}
								{#each vehicleMakes as pItem}
									<option value={pItem.id}>{pItem.name}</option>
								{/each}
							{/if}
						</select>
					</Field.Content>
				</Field.Field>

				{#if activeTab === 'ACTIVITY_OUTCOME'}
					<Field.Field class="w-full flex items-center gap-2">
						<Field.Content>
							<label class="flex items-center gap-2 text-sm font-medium leading-none cursor-pointer">
								<input
									type="checkbox"
									bind:checked={itemIsPositive}
									class="h-4 w-4 rounded border-input text-primary focus:ring-primary"
								/>
								Is Positive Outcome
							</label>
						</Field.Content>
					</Field.Field>
				{/if}
			{/if}

			{#if activeTab === 'WHATSAPP_IMAGE'}
				<!-- Image Upload or URL Selection -->
				<div class="grid grid-cols-1 gap-4 border border-border bg-muted/10 p-3.5 rounded-xl">
					<div class="flex items-center justify-between mb-1">
						<span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Image Source</span>
					</div>

					<div class="space-y-1.5 flex flex-col">
						<span class="text-xs text-muted-foreground font-medium">Upload File (Converts to Base64)</span>
						{#if !imageInputBase64}
							<label
								class="flex flex-col items-center justify-center h-24 border border-dashed border-border rounded-xl cursor-pointer hover:bg-muted/30 transition-colors"
							>
								<Icon name="upload" class="size-5 text-muted-foreground/60 mb-1" />
								<span class="text-xs text-muted-foreground">Select local image</span>
								<input
									type="file"
									accept="image/*"
									class="hidden"
									onchange={handleImageUpload}
								/>
							</label>
						{:else}
							<div class="relative h-24 border border-border rounded-xl overflow-hidden bg-card flex items-center justify-center">
								<img
									src={imageLocalPreview}
									alt="Preview"
									class="h-full w-full object-contain p-1"
								/>
								<button
									type="button"
									onclick={clearUploadedImage}
									class="absolute top-1.5 right-1.5 p-1 bg-rose-500 hover:bg-rose-600 text-white rounded-lg transition-all shadow-md"
									title="Clear image"
								>
									<Icon name="trash" class="size-3" />
								</button>
							</div>
						{/if}
					</div>

					<div class="relative flex py-1 items-center">
						<div class="flex-grow border-t border-border"></div>
						<span class="flex-shrink mx-3 text-xs text-muted-foreground">OR</span>
						<div class="flex-grow border-t border-border"></div>
					</div>

					<Field.Field class="w-full">
						<Field.Label for="image-url">Image URL</Field.Label>
						<Field.Content>
							<Input
								id="image-url"
								bind:value={imageInputUrl}
								placeholder="https://example.com/image.jpg"
								autocomplete="off"
								class="rounded-xl"
							/>
						</Field.Content>
					</Field.Field>

					<div class="mt-3">
						<MasterSelect
							form={dummyForm}
							fieldName="products"
							masterType="items"
							itemCategoriesFilter={['RETD', 'ECOMILE']}
							label="Linked Items"
							placeholder="Search items..."
							singleSelect={false}
						/>
					</div>
				</div>
			{:else}
				{#if activeTab === 'WHATSAPP_TEMPLATE'}
					<!-- Language and messageText selection -->
					<Field.Field class="w-full">
						<Field.Label for="template-language">Language</Field.Label>
						<Field.Content>
							<Input
								id="template-language"
								bind:value={templateLanguage}
								placeholder="e.g., English, Hindi, Marathi"
								autocomplete="off"
								class="rounded-xl"
							/>
						</Field.Content>
					</Field.Field>

					<Field.Field class="w-full">
						<Field.Label for="template-text">Message Template Text</Field.Label>
						<Field.Content>
							<Textarea
								id="template-text"
								bind:value={templateMessageText}
								placeholder="Type WhatsApp message contents here..."
								class="min-h-[120px] rounded-xl"
							/>
						</Field.Content>
					</Field.Field>
				{:else if activeTab === 'CRM_PRODUCTS'}
					<div class="space-y-3">
						<MasterSelect
							form={productForm}
							fieldName="code"
							masterType="items"
							label="Product Code / Item"
							placeholder="Select product code / item..."
							singleSelect={true}
							onPicked={(detail) => {
								if (detail.meta) {
									const cat = String(detail.meta.itemCategoryCode ?? '').trim();
									const grp = String(detail.meta.productGroupCode ?? '').trim();
									if (cat) productFormValues.category = cat;
									if (grp) productFormValues.productGroup = grp;
								}
								if (detail.value) {
									fetchAndPrefillPrice(detail.value, productFormValues.respCenters);
								}
							}}
						/>

						<MasterSelect
							form={productForm}
							fieldName="category"
							masterType="itemCategories"
							label="Category"
							placeholder="Select category..."
							singleSelect={true}
						/>

						<MasterSelect
							form={productForm}
							fieldName="productGroup"
							masterType="productGroups"
							label="Product Group"
							placeholder="Select product group..."
							singleSelect={true}
						/>

						<Field.Field class="w-full">
							<Field.Label for="product-final-price">Final Price (₹)</Field.Label>
							<Field.Content>
								<Input
									id="product-final-price"
									type="number"
									bind:value={productFormValues.finalPrice}
									placeholder="e.g. 11484"
									autocomplete="off"
									class="rounded-xl"
								/>
							</Field.Content>
						</Field.Field>

						<MasterSelect
							form={productForm}
							fieldName="respCenters"
							masterType="respCenters"
							respCenterType="Sale"
							label="Responsibility Centers"
							placeholder="Select Resp Centers..."
							singleSelect={false}
							onPicked={() => {
								if (productFormValues.code) {
									fetchAndPrefillPrice(productFormValues.code, productFormValues.respCenters);
								}
							}}
						/>

						<Field.Field class="w-full">
							<Field.Label for="product-whatsapp-image">Linked WhatsApp Image</Field.Label>
							<Field.Content>
								<select
									id="product-whatsapp-image"
									bind:value={productFormValues.whatsappImageCode}
									class="flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
								>
									<option value="">-- None (No Image Linked) --</option>
									{#each imagesList.items as img}
										<option value={img.name}>{img.name}</option>
									{/each}
								</select>
							</Field.Content>
						</Field.Field>
					</div>
				{/if}
			{/if}
		</div>

		<Dialog.Footer class="flex gap-2 justify-end pt-4 border-t">
			<Button
				type="button"
				variant="outline"
				disabled={isSaving}
				onclick={() => (dialogOpen = false)}
				class="rounded-xl"
			>
				Cancel
			</Button>
			<Button
				type="button"
				disabled={(activeTab === 'CRM_PRODUCTS' ? !productFormValues.code : !itemNameInput.trim()) || isSaving}
				onclick={saveItem}
				class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl gap-2 shadow-lg hover:shadow-indigo-500/10"
			>
				{#if isSaving}
					<Loader2 class="size-4 animate-spin shrink-0" />
				{/if}
				{dialogMode === 'add' ? 'Create' : 'Save Changes'}
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Delete Confirmation Modal -->
<Dialog.Root bind:open={deleteDialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>Delete item</Dialog.Title>
		</Dialog.Header>

		<div class="py-3">
			<p class="text-sm text-muted-foreground leading-relaxed">
				Are you sure you want to delete <strong class="text-foreground">"{deleteItemName}"</strong>? This action cannot be undone and may affect associated records.
			</p>
		</div>

		<Dialog.Footer class="flex gap-2 justify-end pt-4 border-t">
			<Button
				type="button"
				variant="outline"
				disabled={isDeleting}
				onclick={() => (deleteDialogOpen = false)}
				class="rounded-xl"
			>
				Cancel
			</Button>
			<Button
				type="button"
				disabled={isDeleting}
				onclick={confirmDelete}
				class="bg-rose-600 hover:bg-rose-500 text-white rounded-xl gap-2 shadow-lg hover:shadow-rose-500/10"
			>
				{#if isDeleting}
					<Loader2 class="size-4 animate-spin shrink-0" />
				{/if}
				Delete Item
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<style>
	:global(.scrollbar-hide::-webkit-scrollbar) {
		display: none;
	}
	:global(.scrollbar-hide) {
		-ms-overflow-style: none;
		scrollbar-width: none;
	}
	:global(.animate-spin) {
		animation: spin 1s linear infinite;
	}
	@keyframes spin {
		from {
			transform: rotate(0deg);
		}
		to {
			transform: rotate(360deg);
		}
	}
</style>
