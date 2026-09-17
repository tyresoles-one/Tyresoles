<script lang="ts">
	import { untrack } from 'svelte';
	import { page } from '$app/stores';
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
	import CrmDailyCallTargetsView from './CrmDailyCallTargetsView.svelte';

	import { graphqlQuery, graphqlMutation, buildMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	type CrmMasterItem = {
		id: number;
		code?: string | null;
		name: string;
		parentId?: number | null;
		isPositive?: boolean;
		description?: string | null;
		isActive?: boolean;
	};

	type CrmMasterType =
		| 'CONTACT_TYPE'
		| 'CONTACT_CATEGORY'
		| 'SOURCE'
		| 'SOURCE_CHANNEL'
		| 'STAGE'
		| 'PRIORITY'
		| 'ACTIVITY_TYPE'
		| 'ACTIVITY_OUTCOME'
		| 'CALL_TARGETS'
		| 'WHATSAPP_IMAGE'
		| 'WHATSAPP_TEMPLATE'
		| 'CRM_PRODUCTS'
		| 'ENTITY_TYPE'
		| 'APPLICATION'
		| 'VEHICLE_MAKE'
		| 'VEHICLE_MODEL'
		| 'VEHICLE_TYPE'
		| 'LANGUAGE'
		| 'CRM_SETTINGS';


	type MasterCategory = 'all' | 'leads' | 'pipeline' | 'fleet' | 'media' | 'system';

	interface MasterTypeItem {
		type: CrmMasterType;
		label: string;
		category: MasterCategory;
		icon: string;
		badge?: string;
		description: string;
	}

	type CrmProduct = {
		id: string;
		code: string;
		category?: string | null;
		productGroup?: string | null;
		finalPrice: number;
		respCenters?: string | null;
		whatsappImageCode?: string | null;
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
		languageCode?: string | null;
		messageText: string;
		createdAt: string;
	};

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				code
				name
				parentId
				isPositive
				description
				isActive
			}
		}
	` as unknown as TypedDocumentNode<CrmMasterItemsResult, { type: CrmMasterType; where?: any }>;

	const CreateCrmMasterItemDocument = buildMutation`
		mutation CreateCrmMasterItem($type: CrmMasterType!, $name: String!, $code: String, $parentId: Int, $isPositive: Boolean, $description: String, $isActive: Boolean) {
			createCrmMasterItem(type: $type, name: $name, code: $code, parentId: $parentId, isPositive: $isPositive, description: $description, isActive: $isActive) {
				id
				code
				name
				parentId
				isPositive
				description
				isActive
			}
		}
	` as unknown as TypedDocumentNode<CreateItemResult, { type: CrmMasterType; name: string; code?: string | null; parentId?: number | null; isPositive?: boolean; description?: string | null; isActive?: boolean }>;

	const UpdateCrmMasterItemDocument = buildMutation`
		mutation UpdateCrmMasterItem($type: CrmMasterType!, $id: Int!, $name: String!, $code: String, $parentId: Int, $isPositive: Boolean, $description: String, $isActive: Boolean) {
			updateCrmMasterItem(type: $type, id: $id, name: $name, code: $code, parentId: $parentId, isPositive: $isPositive, description: $description, isActive: $isActive) {
				id
				code
				name
				parentId
				isPositive
				description
				isActive
			}
		}
	` as unknown as TypedDocumentNode<UpdateItemResult, { type: CrmMasterType; id: number; name: string; code?: string | null; parentId?: number | null; isPositive?: boolean; description?: string | null; isActive?: boolean }>;

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
				languageCode
				messageText
				createdAt
			}
		}
	` as unknown as TypedDocumentNode<{ templates: CrmWhatsappTemplate[] }, {}>;

	const SaveCrmWhatsappTemplateDocument = buildMutation`
		mutation SaveCrmWhatsappTemplate($input: CrmWhatsappTemplateInput!) {
			saveCrmWhatsappTemplate(input: $input) {
				id
				name
				language
				languageCode
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

	// Category Definitions
	const CATEGORIES: { id: MasterCategory; label: string; icon: string; count: number }[] = [
		{ id: 'all', label: 'All', icon: 'layers', count: 18 },
		{ id: 'leads', label: 'Leads & Contacts', icon: 'users', count: 5 },
		{ id: 'pipeline', label: 'Sales Pipeline', icon: 'git-merge', count: 4 },
		{ id: 'fleet', label: 'Fleet Taxonomy', icon: 'truck', count: 4 },
		{ id: 'media', label: 'Catalog & Media', icon: 'message-square', count: 4 },
		{ id: 'system', label: 'Settings', icon: 'settings', count: 1 }
	];

	// Complete 18 CRM Master Types
	const lookupTypes: MasterTypeItem[] = [
		// ─── Leads & Contacts ───
		{
			type: 'CONTACT_TYPE',
			label: 'Contact Types',
			category: 'leads',
			icon: 'user-cog',
			description: 'Relationship designations for contacts (e.g. Owner, Purchase Manager, Plant Incharge).'
		},
		{
			type: 'CONTACT_CATEGORY',
			label: 'Contact Categories',
			category: 'leads',
			icon: 'tags',
			description: 'Categorize contacts by tier or value status (e.g. VIP, Regular, High-Priority).'
		},
		{
			type: 'ENTITY_TYPE',
			label: 'Entity Types',
			category: 'leads',
			icon: 'building-2',
			description: 'Classify business entities (e.g. Fleet Operator, Transporter, Dealer, Broker).'
		},
		{
			type: 'SOURCE',
			label: 'Lead Sources',
			category: 'leads',
			icon: 'share-2',
			description: 'High-level lead sources (e.g. Automated, Inbound, Referral, Campaign, Trade Show).'
		},
		{
			type: 'SOURCE_CHANNEL',
			label: 'Source Channels',
			category: 'leads',
			icon: 'radio',
			badge: 'New',
			description: 'Specific discovery channels (e.g. Web-Harvester, Google-Maps, IndiaMART, WhatsApp, Direct-Call).'
		},

		// ─── Sales Pipeline ───
		{
			type: 'STAGE',
			label: 'Pipeline Stages',
			category: 'pipeline',
			icon: 'git-merge',
			description: 'Define opportunity lifecycle stages (e.g. Lead, Contacted, Qualified, Closed-Won).'
		},
		{
			type: 'PRIORITY',
			label: 'Deal Priorities',
			category: 'pipeline',
			icon: 'alert-circle',
			description: 'Set urgency levels for sales deals, calling queues, and follow-up schedules.'
		},
		{
			type: 'ACTIVITY_TYPE',
			label: 'Activity Types',
			category: 'pipeline',
			icon: 'phone-call',
			description: 'Sales interaction channels (e.g. Tele-Call, Yard Audit, Office Visit, Tyre Inspection).'
		},
		{
			type: 'ACTIVITY_OUTCOME',
			label: 'Activity Outcomes',
			category: 'pipeline',
			icon: 'check-square',
			description: 'Standardized outcomes from agent calls, field visits, and audit meetings.'
		},
		{
			type: 'CALL_TARGETS',
			label: 'Daily Call Targets',
			category: 'pipeline',
			icon: 'crosshair',
			badge: 'New',
			description: 'Configure mandatory daily, weekly, and monthly calling targets per agent or team default.'
		},

		// ─── Fleet Taxonomy ───

		{
			type: 'VEHICLE_TYPE',
			label: 'Vehicle Types',
			category: 'fleet',
			icon: 'car',
			description: 'Primary commercial vehicle classifications (e.g. Multi-Axle Truck, Bus, Trailer, Tipper).'
		},
		{
			type: 'VEHICLE_MAKE',
			label: 'Vehicle Makes',
			category: 'fleet',
			icon: 'truck',
			description: 'Vehicle manufacturers (e.g. Tata, Ashok Leyland, BharatBenz, Eicher, Mahindra).'
		},
		{
			type: 'VEHICLE_MODEL',
			label: 'Vehicle Models',
			category: 'fleet',
			icon: 'cog',
			description: 'Commercial vehicle models mapped under specific manufacturer makes.'
		},
		{
			type: 'APPLICATION',
			label: 'Fleet Applications',
			category: 'fleet',
			icon: 'briefcase',
			description: 'Operating application segments (e.g. Long Haul, Regional, Mining, Overburden, Cement).'
		},

		// ─── Catalog & Media ───
		{
			type: 'CRM_PRODUCTS',
			label: 'CRM Products',
			category: 'media',
			icon: 'package',
			description: 'Manage sales item codes, price matrix, and responsibility center assignments.'
		},
		{
			type: 'WHATSAPP_TEMPLATE',
			label: 'WhatsApp Templates',
			category: 'media',
			icon: 'message-square',
			description: 'Pre-approved message templates for quick agent outreach and automated marketing.'
		},
		{
			type: 'WHATSAPP_IMAGE',
			label: 'WhatsApp Media',
			category: 'media',
			icon: 'image',
			description: 'Pre-saved retreading marketing flyers, before/after photos, and media assets.'
		},
		{
			type: 'LANGUAGE',
			label: 'Languages',
			category: 'media',
			icon: 'languages',
			description: 'Configure supported languages and ISO codes (e.g. en-English, hi-Hindi, mr-Marathi).'
		},

		// ─── System ───
		{
			type: 'CRM_SETTINGS',
			label: 'CRM Settings',
			category: 'system',
			icon: 'settings',
			description: 'Global CRM and ERP synchronization rules, price group mappings, and agent parameters.'
		}
	];

	// ─── Reactive Page States ───
	let activeTab = $state<CrmMasterType>('CONTACT_TYPE');
	let activeCategory = $state<MasterCategory>('all');
	let masterSearchFilter = $state('');
	let viewMode = $state<'grid' | 'table'>('grid');
	let isSidebarExpanded = $state(true);

	function syncUrl(tab: CrmMasterType, cat: MasterCategory) {
		if (typeof window !== 'undefined') {
			const url = new URL(window.location.href);
			url.searchParams.set('tab', tab);
			if (cat !== 'all') {
				url.searchParams.set('category', cat);
			} else {
				url.searchParams.delete('category');
			}
			window.history.replaceState({}, '', url.toString());
		}
	}

	function selectTab(type: CrmMasterType) {
		activeTab = type;
		const found = lookupTypes.find((t) => t.type === type);
		if (found && activeCategory !== 'all' && found.category !== activeCategory) {
			activeCategory = found.category;
		}
		syncUrl(type, activeCategory);
	}

	function selectCategory(cat: MasterCategory) {
		activeCategory = cat;
		const inCat = lookupTypes.filter((m) => cat === 'all' || m.category === cat);
		if (!inCat.some((m) => m.type === activeTab) && inCat.length > 0) {
			activeTab = inCat[0].type;
		}
		syncUrl(activeTab, cat);
	}

	// Two-way deep linking: react to URL query parameters
	$effect(() => {
		const searchParams = $page.url.searchParams;
		const tabParam = searchParams.get('tab') || searchParams.get('type');
		const catParam = searchParams.get('category');

		if (tabParam) {
			const found = lookupTypes.find((t) => t.type.toUpperCase() === tabParam.toUpperCase());
			if (found && found.type !== activeTab) {
				activeTab = found.type;
				if (activeCategory !== 'all' && activeCategory !== found.category) {
					activeCategory = found.category;
				}
			}
		} else if (catParam) {
			const cat = catParam.toLowerCase() as MasterCategory;
			if (CATEGORIES.some((c) => c.id === cat) && cat !== activeCategory) {
				activeCategory = cat;
				const inCat = lookupTypes.filter((m) => cat === 'all' || m.category === cat);
				if (!inCat.some((m) => m.type === activeTab) && inCat.length > 0) {
					activeTab = inCat[0].type;
				}
			}
		}
	});

	// Filtered masters list for sidebar & switcher
	const filteredLookupTypes = $derived.by(() => {
		let list = lookupTypes;
		if (activeCategory !== 'all') {
			list = list.filter((m) => m.category === activeCategory);
		}
		const q = masterSearchFilter.trim().toLowerCase();
		if (q) {
			list = list.filter(
				(m) =>
					m.label.toLowerCase().includes(q) ||
					m.description.toLowerCase().includes(q) ||
					m.type.toLowerCase().includes(q)
			);
		}
		return list;
	});

	const activeConfig = $derived(lookupTypes.find((x) => x.type === activeTab) || lookupTypes[0]);

	// ─── Data Lists ───
	const lookupList = usePaginatedList<CrmMasterItem>({
		query: GetCrmMasterItemsDocument,
		dataPath: 'crmMasterItems',
		itemsPath: 'crmMasterItems',
		countPath: 'crmMasterItems.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			type: activeTab === 'CRM_SETTINGS' ? 'CONTACT_TYPE' : activeTab,
			where: term
				? activeTab === 'LANGUAGE' || activeTab === 'SOURCE_CHANNEL'
					? { or: [{ name: { contains: term } }, { code: { contains: term } }] }
					: { name: { contains: term } }
				: null
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

	// ─── Parent Lookups ───
	let activityTypes = $state<CrmMasterItem[]>([]);
	let vehicleTypes = $state<CrmMasterItem[]>([]);
	let vehicleMakes = $state<CrmMasterItem[]>([]);
	let availableSources = $state<CrmMasterItem[]>([]);
	let availableLanguages = $state<CrmMasterItem[]>([]);

	async function loadActivityTypes() {
		const res = await graphqlQuery<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
			variables: { type: 'ACTIVITY_TYPE' }
		});
		if (res.success && res.data) {
			activityTypes = res.data.crmMasterItems;
		}
	}

	async function loadVehicleTypes() {
		const res = await graphqlQuery<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
			variables: { type: 'VEHICLE_TYPE' }
		});
		if (res.success && res.data) {
			vehicleTypes = res.data.crmMasterItems;
		}
	}

	async function loadVehicleMakes() {
		const res = await graphqlQuery<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
			variables: { type: 'VEHICLE_MAKE' }
		});
		if (res.success && res.data) {
			vehicleMakes = res.data.crmMasterItems;
		}
	}

	async function loadSources() {
		try {
			const res = await graphqlQuery<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
				variables: { type: 'SOURCE' }
			});
			if (res.success && res.data?.crmMasterItems) {
				availableSources = res.data.crmMasterItems;
			}
		} catch (e) {
			console.error('Failed to load sources', e);
		}
	}

	async function loadLanguages() {
		try {
			const res = await graphqlQuery<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
				variables: { type: 'LANGUAGE' }
			});
			if (res.success && res.data?.crmMasterItems) {
				availableLanguages = res.data.crmMasterItems;
			}
		} catch (e) {
			console.error('Failed to load languages', e);
		}
	}

	// Startup loads
	loadLanguages();
	loadSources();

	let dummyForm = $state({
		values: { products: '' as string },
		setTouched: (_name: string) => {},
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
			} else if (tab === 'SOURCE_CHANNEL') {
				loadSources();
			} else if (tab === 'WHATSAPP_TEMPLATE') {
				loadLanguages();
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
				const search = lookupList.searchQuery.value;
				let whereClause = null;
				if (search) {
					whereClause =
						tab === 'LANGUAGE' || tab === 'SOURCE_CHANNEL'
							? { or: [{ name: { contains: search } }, { code: { contains: search } }] }
							: { name: { contains: search } };
				}
				lookupList.pagination.setVariables({
					type: tab,
					where: whereClause
				});
				lookupList.onRefresh();
			}
		});
	});

	// ─── Dialog States ───
	let dialogOpen = $state(false);
	let dialogMode = $state<'add' | 'edit'>('add');
	let editItemId = $state<number | null>(null);
	let editItemGuid = $state<string | null>(null);
	let itemNameInput = $state('');
	let itemCodeInput = $state('');
	let itemParentId = $state<number | null>(null);
	let itemIsPositive = $state(false);
	let itemDescriptionInput = $state('');
	let itemIsActive = $state(true);
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
		respCenters: '',
		whatsappImageCode: ''
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
			const settingRes = await graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(
				GetCrmSettingDocument,
				{
					variables: { key: 'CUSTOMER_PRICE_GROUP_MAPPING' }
				}
			);
			if (settingRes.success && settingRes.data?.getCrmSetting?.value) {
				const mappings: { respCenters: string[]; priceGroupCode: string }[] = JSON.parse(
					settingRes.data.getCrmSetting.value
				);
				if (respCentersStr) {
					const rcList = respCentersStr.split(',').map((r) => r.trim().toLowerCase()).filter(Boolean);
					const match = mappings.find((m) => m.respCenters?.some((rc) => rcList.includes(rc.trim().toLowerCase())));
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

	// Deletion states
	let deleteDialogOpen = $state(false);
	let deleteItemId = $state<number | null>(null);
	let deleteItemGuid = $state<string | null>(null);
	let deleteItemName = $state('');
	let isDeleting = $state(false);

	function openAddDialog() {
		dialogMode = 'add';
		editItemId = null;
		editItemGuid = null;
		itemNameInput = '';
		itemCodeInput = '';
		itemParentId = null;
		itemIsPositive = false;
		itemDescriptionInput = '';
		itemIsActive = true;

		imageInputUrl = '';
		imageInputBase64 = '';
		imageLocalFile = null;
		imageLocalPreview = '';
		dummyForm.values.products = '';

		templateLanguage = availableLanguages.length > 0 ? availableLanguages[0].name : 'English';
		templateMessageText = '';

		productFormValues = {
			code: '',
			category: '',
			productGroup: '',
			finalPrice: 0,
			respCenters: '',
			whatsappImageCode: ''
		};

		if (activeTab === 'SOURCE_CHANNEL') {
			loadSources();
		}

		dialogOpen = true;
	}

	function openEditDialog(item: any) {
		dialogMode = 'edit';
		itemNameInput = item.name || item.code || '';
		itemCodeInput = item.code || '';
		itemDescriptionInput = item.description || '';
		itemIsActive = item.isActive ?? true;

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

		if (activeTab === 'SOURCE_CHANNEL') {
			loadSources();
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
				const matchedLang = availableLanguages.find((l) => l.name === templateLanguage || l.code === templateLanguage);
				const input: any = {
					id: editItemGuid || null,
					name,
					language: templateLanguage.trim() || 'English',
					languageCode: matchedLang?.code || null,
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
				const code = itemCodeInput.trim();
				if (activeTab === 'LANGUAGE' && !code) {
					toast.error('Language code cannot be empty (e.g. en, hi, mr)');
					isSaving = false;
					return;
				}

				const description = itemDescriptionInput.trim() || null;

				if (dialogMode === 'add') {
					const res = await graphqlMutation<CreateItemResult>(CreateCrmMasterItemDocument, {
						variables: {
							type: activeTab,
							name,
							code: activeTab === 'LANGUAGE' || activeTab === 'SOURCE_CHANNEL' ? code || null : null,
							parentId: itemParentId,
							isPositive: itemIsPositive,
							description: description,
							isActive: itemIsActive
						}
					});

					if (res.success && res.data?.createCrmMasterItem) {
						toast.success(`"${name}" added successfully.`);
						dialogOpen = false;
						lookupList.onRefresh();
						if (activeTab === 'LANGUAGE') loadLanguages();
						if (activeTab === 'SOURCE') loadSources();
					} else {
						toast.error(res.error || 'Failed to add item');
					}
				} else {
					if (editItemId === null) return;
					const res = await graphqlMutation<UpdateItemResult>(UpdateCrmMasterItemDocument, {
						variables: {
							type: activeTab,
							id: editItemId,
							name,
							code: activeTab === 'LANGUAGE' || activeTab === 'SOURCE_CHANNEL' ? code || null : null,
							parentId: itemParentId,
							isPositive: itemIsPositive,
							description: description,
							isActive: itemIsActive
						}
					});

					if (res.success && res.data?.updateCrmMasterItem) {
						toast.success(`Item updated to "${name}".`);
						dialogOpen = false;
						lookupList.onRefresh();
						if (activeTab === 'LANGUAGE') loadLanguages();
						if (activeTab === 'SOURCE') loadSources();
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
					if (activeTab === 'LANGUAGE') loadLanguages();
					if (activeTab === 'SOURCE') loadSources();
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
	<title>{activeConfig.label} | CRM Masters | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-slate-50/50 dark:bg-background text-foreground pb-20">
	<div class="max-w-[1520px] mx-auto px-3 sm:px-6 lg:px-8 pt-6">
		<!-- ─── Top Control & Domain Header ─── -->
		<div class="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6 bg-card border border-border/80 rounded-2xl p-4 sm:p-5 shadow-xs">
			<div class="space-y-1">
				<div class="flex items-center gap-2.5">
					<div class="p-2 rounded-xl bg-primary/10 text-primary">
						<Icon name="database" class="size-5" />
					</div>
					<div>
						<h1 class="text-lg sm:text-xl font-bold tracking-tight text-foreground flex items-center gap-2">
							CRM Masters & Reference Tables
							<span class="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-zinc-800 text-muted-foreground border border-border">
								18 Tables
							</span>
						</h1>
						<p class="text-xs text-muted-foreground line-clamp-1">
							Configure contact categories, sources & channels, stages, fleet taxonomy, and messaging templates.
						</p>
					</div>
				</div>
			</div>

			<!-- Quick Category Filter Pills -->
			<div class="flex flex-wrap items-center gap-1.5 p-1 bg-muted/50 dark:bg-zinc-900/50 rounded-xl border border-border/60">
				{#each CATEGORIES as cat}
					<button
						type="button"
						onclick={() => selectCategory(cat.id)}
						class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all select-none
							{activeCategory === cat.id
								? 'bg-background text-foreground shadow-xs border border-border/80 font-semibold'
								: 'text-muted-foreground hover:text-foreground hover:bg-background/50'}"
					>
						<Icon name={cat.icon} class="size-3.5" />
						<span>{cat.label}</span>
						<span class="text-[10px] opacity-60">({cat.count})</span>
					</button>
				{/each}
			</div>
		</div>

		<!-- ─── Mobile/Tablet Quick Master Selector ─── -->
		<div class="lg:hidden mb-5 flex flex-col gap-2 bg-card border border-border p-3.5 rounded-xl shadow-xs">
			<label for="mobile-master-select" class="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center justify-between">
				<span>Selected Master Table</span>
				<span class="text-[10px] text-primary font-normal">{activeConfig.label}</span>
			</label>
			<select
				id="mobile-master-select"
				value={activeTab}
				onchange={(e) => selectTab(e.currentTarget.value as CrmMasterType)}
				class="w-full h-10 px-3 bg-background border border-border rounded-xl text-sm font-medium focus:ring-2 focus:ring-primary shadow-xs"
			>
				{#each filteredLookupTypes as item}
					<option value={item.type}>
						{item.label} ({item.category})
					</option>
				{/each}
			</select>
		</div>

		<!-- ─── Main Two-Column Layout ─── -->
		<div class="flex flex-col lg:flex-row gap-6 items-start">
			<!-- ─── Master Selection Sidebar (Desktop) ─── -->
			<aside
				class="hidden lg:block shrink-0 transition-all duration-300 ease-in-out {isSidebarExpanded ? 'w-80' : 'w-[76px]'}"
			>
				<div class="sticky top-20 max-h-[calc(100vh-6.5rem)] flex flex-col bg-card border border-border/90 rounded-2xl shadow-xs overflow-hidden">
					<!-- Sidebar Header & Search -->
					<div class="p-3.5 border-b border-border space-y-2.5 bg-muted/20">
						<div class="flex items-center justify-between">
							{#if isSidebarExpanded}
								<div class="flex items-center gap-2">
									<span class="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
										Categories
									</span>
									<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted text-muted-foreground">
										{filteredLookupTypes.length}
									</span>
								</div>
							{/if}
							<button
								type="button"
								class="p-1.5 rounded-lg hover:bg-muted text-muted-foreground hover:text-foreground transition-colors {isSidebarExpanded ? '' : 'mx-auto'}"
								onclick={() => (isSidebarExpanded = !isSidebarExpanded)}
								title={isSidebarExpanded ? 'Collapse Sidebar' : 'Expand Sidebar'}
							>
								<Icon name={isSidebarExpanded ? 'panel-left-close' : 'panel-left-open'} class="size-4" />
							</button>
						</div>

						{#if isSidebarExpanded}
							<div class="relative">
								<Icon name="search" class="size-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
								<Input
									type="text"
									placeholder="Filter masters..."
									bind:value={masterSearchFilter}
									class="h-8 pl-8 pr-7 text-xs rounded-lg border-border/80 bg-background"
								/>
								{#if masterSearchFilter}
									<button
										type="button"
										onclick={() => (masterSearchFilter = '')}
										class="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground text-xs"
									>
										✕
									</button>
								{/if}
							</div>
						{/if}
					</div>

					<!-- Master Items List -->
					<div class="p-2 overflow-y-auto flex flex-col gap-1 scrollbar-hide flex-1">
						{#if filteredLookupTypes.length === 0}
							<div class="py-8 text-center text-xs text-muted-foreground">
								No masters matching "{masterSearchFilter}"
							</div>
						{:else}
							{#each filteredLookupTypes as item}
								{@const isActive = activeTab === item.type}
								<button
									type="button"
									title={!isSidebarExpanded ? item.label : undefined}
									onclick={() => selectTab(item.type)}
									class="w-full flex items-center gap-3 rounded-xl transition-all text-left group relative select-none
										{isSidebarExpanded ? 'px-3 py-2.5' : 'p-3 justify-center'}
										{isActive
											? 'bg-primary/10 border border-primary/30 text-primary font-semibold shadow-2xs'
											: 'border border-transparent text-muted-foreground hover:text-foreground hover:bg-muted/60'}"
								>
									<div
										class="p-1.5 rounded-lg shrink-0 transition-colors duration-200
											{isActive
												? 'bg-primary text-primary-foreground shadow-xs'
												: 'bg-muted text-muted-foreground group-hover:bg-muted/80 group-hover:text-foreground'}"
									>
										<Icon name={item.icon} class="size-4" />
									</div>

									{#if isSidebarExpanded}
										<div class="min-w-0 flex-1">
											<div class="flex items-center justify-between gap-1">
												<span class="text-xs truncate">{item.label}</span>
												{#if item.badge}
													<span class="text-[9px] font-bold px-1.5 py-0.2 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 border border-emerald-300/40">
														{item.badge}
													</span>
												{/if}
											</div>
											<p class="text-[10px] text-muted-foreground truncate mt-0.5 font-normal">
												{item.description}
											</p>
										</div>
									{/if}
								</button>
							{/each}
						{/if}
					</div>
				</div>
			</aside>

			<!-- ─── Main Content Area ─── -->
			<main class="flex-1 min-w-0 w-full">
				{#if activeTab === 'CRM_SETTINGS'}
					<CrmSettingsView />
				{:else if activeTab === 'CALL_TARGETS'}
					<CrmDailyCallTargetsView />
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
								class="gap-1.5 shrink-0 bg-indigo-600 hover:bg-indigo-700 text-white font-medium shadow-sm rounded-xl px-3.5 py-2 text-xs transition-all"
								onclick={openAddDialog}
							>
								<Icon name="plus" class="size-3.5" />
								<span>Add {activeConfig.label.endsWith('s') ? activeConfig.label.slice(0, -1) : activeConfig.label}</span>
							</Button>
						{/snippet}

						<!-- ─── Card Grid Item View ─── -->
						{#snippet gridItem(item: any)}
							{@const isTemplate = activeTab === 'WHATSAPP_TEMPLATE'}
							{@const isImage = activeTab === 'WHATSAPP_IMAGE'}
							{@const isProduct = activeTab === 'CRM_PRODUCTS'}
							{@const isChannel = activeTab === 'SOURCE_CHANNEL'}

							<div class="h-full rounded-xl border border-border/80 bg-card hover:bg-accent/5 backdrop-blur-xs p-4 relative group flex flex-col justify-between transition-all duration-200 hover:shadow-md hover:border-primary/30">
								<div class="flex flex-col gap-3 h-full justify-between">
									<div class="space-y-3">
										<!-- WhatsApp Image View -->
										{#if isImage}
											<div class="aspect-video w-full rounded-xl bg-slate-50 dark:bg-zinc-900/50 flex items-center justify-center overflow-hidden border border-border/80 shadow-2xs relative group-hover:border-blue-500/20 transition-all duration-300">
												{#if item.base64Data || item.imageUrl}
													<img src={item.base64Data || item.imageUrl} alt="" class="absolute inset-0 h-full w-full object-cover blur-md opacity-25 dark:opacity-15 scale-110 pointer-events-none" />
													<img src={item.base64Data || item.imageUrl} alt={item.name} class="h-full w-full object-contain relative z-10 transition-transform duration-500 group-hover:scale-105 p-1" />
												{:else}
													<div class="flex flex-col items-center gap-1 text-muted-foreground/40">
														<Icon name="image" class="size-8" />
														<span class="text-[10px]">No image uploaded</span>
													</div>
												{/if}
											</div>
										{/if}

										<!-- CRM Product View -->
										{#if isProduct}
											<div class="flex items-center justify-between gap-2 w-full">
												<div class="p-2 rounded-xl border bg-indigo-500/10 border-indigo-500/20 text-indigo-600 dark:text-indigo-400">
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
										{:else if isChannel}
											<!-- ─── Source Channel Grid Card ─── -->
											<div class="flex items-start justify-between gap-2">
												<div class="flex items-center gap-2.5">
													<div class="p-2 rounded-xl bg-indigo-50 dark:bg-indigo-950/50 border border-indigo-200 dark:border-indigo-900 text-indigo-600 dark:text-indigo-400">
														<Icon name="radio" class="size-4" />
													</div>
													<div>
														<h3 class="font-semibold text-sm text-foreground group-hover:text-primary transition-colors">
															{item.name}
														</h3>
														<div class="flex items-center gap-1.5 mt-0.5">
															<span class="text-[10px] font-mono text-muted-foreground">
																#{item.id}
															</span>
															{#if item.code}
																<span class="inline-flex items-center px-1.5 py-0.2 rounded text-[10px] font-mono font-semibold bg-primary/10 text-primary border border-primary/20">
																	{item.code}
																</span>
															{/if}
														</div>
													</div>
												</div>
												<span
													class="text-[10px] font-semibold px-2 py-0.5 rounded-full border
													{item.isActive !== false
														? 'bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-400 border-emerald-300 dark:border-emerald-800'
														: 'bg-slate-100 dark:bg-zinc-800 text-muted-foreground border-border'}"
												>
													{item.isActive !== false ? 'Active' : 'Inactive'}
												</span>
											</div>

											{#if item.description}
												<p class="text-xs text-muted-foreground line-clamp-2 mt-1">
													{item.description}
												</p>
											{/if}

											{#if item.parentId}
												{@const parentSource = availableSources.find((s) => s.id === item.parentId)}
												<div class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[10px] font-medium bg-slate-100 dark:bg-zinc-800 text-foreground/80 w-fit">
													<Icon name="share-2" class="size-3 text-muted-foreground" />
													<span>Parent Source: {parentSource?.name || 'ID ' + item.parentId}</span>
												</div>
											{/if}
										{:else}
											<!-- Standard Masters & WhatsApp Templates -->
											<div class="flex items-start justify-between gap-4">
												<div class="flex items-center gap-3">
													{#if !isImage}
														<div class="p-2 rounded-xl bg-primary/10 border border-primary/20 text-primary">
															<Icon name={activeConfig.icon} class="size-4" />
														</div>
													{/if}
													<div class="min-w-0">
														<h3 class="font-semibold text-sm text-foreground group-hover:text-primary transition-colors truncate">
															{item.name || item.code}
														</h3>
														{#if isTemplate && item.language && item.name.toLowerCase() !== item.language.toLowerCase()}
															<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[10px] font-medium bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 mt-1">
																{item.language}
															</span>
														{/if}
														{#if isImage && item.products}
															<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[10px] font-medium bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400 mt-1">
																<Icon name="package" class="size-2.5" />
																{item.products}
															</span>
														{/if}
														{#if !isTemplate && !isImage}
															<div class="flex items-center gap-1.5 mt-0.5">
																<span class="text-[10px] font-mono text-muted-foreground truncate max-w-[180px]">
																	ID: {item.id}
																</span>
																{#if activeTab === 'LANGUAGE' && item.code}
																	<span class="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-mono font-semibold bg-primary/10 text-primary">
																		{item.code}
																	</span>
																{/if}
															</div>
															{#if activeTab === 'ACTIVITY_OUTCOME' || activeTab === 'VEHICLE_MAKE' || activeTab === 'VEHICLE_MODEL'}
																<div class="flex flex-col gap-1 mt-1">
																	{#if item.parentId}
																		<span class="text-[10px] text-muted-foreground flex items-center gap-1">
																			<Icon name="git-branch" class="size-3" />
																			{#if activeTab === 'ACTIVITY_OUTCOME'}
																				{activityTypes.find((x) => x.id === item.parentId)?.name || 'Unknown Type'}
																			{:else if activeTab === 'VEHICLE_MAKE'}
																				{vehicleTypes.find((x) => x.id === item.parentId)?.name || 'Unknown Type'}
																			{:else if activeTab === 'VEHICLE_MODEL'}
																				{vehicleMakes.find((x) => x.id === item.parentId)?.name || 'Unknown Make'}
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

						<!-- ─── Table Header ─── -->
						{#snippet tableHeader()}
							{#if activeTab === 'WHATSAPP_IMAGE'}
								<TableHead class="w-[80px] text-center text-muted-foreground">Preview</TableHead>
								<TableHead class="text-muted-foreground">Name</TableHead>
								<TableHead class="text-muted-foreground">Source</TableHead>
								<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
							{:else if activeTab === 'CRM_PRODUCTS'}
								<TableHead class="text-muted-foreground">Product Code</TableHead>
								<TableHead class="text-muted-foreground">Category</TableHead>
								<TableHead class="text-muted-foreground">Group</TableHead>
								<TableHead class="text-muted-foreground">Final Price</TableHead>
								<TableHead class="text-muted-foreground">Resp Centers</TableHead>
								<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
							{:else if activeTab === 'SOURCE_CHANNEL'}
								<TableHead class="w-[80px] text-center text-muted-foreground">ID</TableHead>
								<TableHead class="w-[140px] text-muted-foreground">Code</TableHead>
								<TableHead class="text-muted-foreground">Channel Name</TableHead>
								<TableHead class="text-muted-foreground">Parent Source</TableHead>
								<TableHead class="text-muted-foreground">Status</TableHead>
								<TableHead class="text-muted-foreground">Description</TableHead>
								<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
							{:else}
								<TableHead class="w-[80px] text-center text-muted-foreground">ID</TableHead>
								{#if activeTab === 'LANGUAGE'}
									<TableHead class="w-[120px] text-muted-foreground">Code</TableHead>
								{/if}
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
								<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
							{/if}
						{/snippet}

						<!-- ─── Table Row ─── -->
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
									{item.base64Data ? 'Uploaded Base64' : item.imageUrl ? 'External URL' : 'None'}
								</TableCell>
								<TableCell class="text-right p-3">
									<TableActions
										title={item.name}
										actions={[
											{ label: 'Edit', icon: 'edit', onClick: () => openEditDialog(item) },
											{ label: 'Delete', icon: 'trash', onClick: () => openDeleteDialog(item), variant: 'destructive' }
										]}
									/>
								</TableCell>
							{:else if activeTab === 'CRM_PRODUCTS'}
								<TableCell class="font-medium text-foreground">{item.code}</TableCell>
								<TableCell class="text-xs text-muted-foreground">{item.category || '-'}</TableCell>
								<TableCell class="text-xs text-muted-foreground">{item.productGroup || '-'}</TableCell>
								<TableCell class="font-semibold text-xs text-emerald-600 dark:text-emerald-400">
									₹{item.finalPrice?.toLocaleString('en-IN')}
								</TableCell>
								<TableCell class="text-xs text-muted-foreground">{item.respCenters || 'All'}</TableCell>
								<TableCell class="text-right p-3">
									<TableActions
										title={item.code}
										actions={[
											{ label: 'Edit', icon: 'edit', onClick: () => openEditDialog(item) },
											{ label: 'Delete', icon: 'trash', onClick: () => openDeleteDialog(item), variant: 'destructive' }
										]}
									/>
								</TableCell>
							{:else if activeTab === 'SOURCE_CHANNEL'}
								<!-- ─── Source Channel Table Row ─── -->
								<TableCell class="text-center font-mono text-xs text-muted-foreground">{item.id}</TableCell>
								<TableCell class="font-mono text-xs font-semibold text-primary">
									{item.code || '-'}
								</TableCell>
								<TableCell class="font-medium text-foreground">
									<div class="flex items-center gap-2">
										<Icon name="radio" class="size-3.5 text-indigo-500 shrink-0" />
										<span>{item.name}</span>
									</div>
								</TableCell>
								<TableCell class="text-xs text-muted-foreground">
									{#if item.parentId}
										{@const ps = availableSources.find((s) => s.id === item.parentId)}
										<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-slate-100 dark:bg-zinc-800 text-foreground/80 font-medium">
											{ps?.name || 'ID ' + item.parentId}
										</span>
									{:else}
										<span class="text-muted-foreground/60">-</span>
									{/if}
								</TableCell>
								<TableCell class="text-xs">
									{#if item.isActive !== false}
										<span class="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400 font-medium">
											<span class="size-1.5 rounded-full bg-emerald-500"></span> Active
										</span>
									{:else}
										<span class="inline-flex items-center gap-1 text-muted-foreground font-medium">
											<span class="size-1.5 rounded-full bg-slate-400"></span> Inactive
										</span>
									{/if}
								</TableCell>
								<TableCell class="text-xs text-muted-foreground max-w-xs truncate">
									{item.description || '-'}
								</TableCell>
								<TableCell class="text-right p-3">
									<TableActions
										title={item.name}
										actions={[
											{ label: 'Edit', icon: 'edit', onClick: () => openEditDialog(item) },
											{ label: 'Delete', icon: 'trash', onClick: () => openDeleteDialog(item), variant: 'destructive' }
										]}
									/>
								</TableCell>
							{:else}
								<TableCell class="text-center font-mono text-xs text-muted-foreground">{item.id}</TableCell>
								{#if activeTab === 'LANGUAGE'}
									<TableCell class="font-mono text-xs font-semibold text-primary">{item.code || '-'}</TableCell>
								{/if}
								<TableCell class="font-medium text-foreground">{item.name}</TableCell>
								{#if activeTab === 'ACTIVITY_OUTCOME'}
									<TableCell class="text-xs text-muted-foreground">
										{activityTypes.find((x) => x.id === item.parentId)?.name || '-'}
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
										{vehicleTypes.find((x) => x.id === item.parentId)?.name || '-'}
									</TableCell>
								{/if}
								{#if activeTab === 'VEHICLE_MODEL'}
									<TableCell class="text-xs text-muted-foreground">
										{vehicleMakes.find((x) => x.id === item.parentId)?.name || '-'}
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
											{ label: 'Edit', icon: 'edit', onClick: () => openEditDialog(item) },
											{ label: 'Delete', icon: 'trash', onClick: () => openDeleteDialog(item), variant: 'destructive' }
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

<!-- ─── Add/Edit Modal ─── -->
<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>
				{dialogMode === 'add' ? 'Add' : 'Edit'} {activeConfig.label.endsWith('s') ? activeConfig.label.slice(0, -1) : activeConfig.label}
			</Dialog.Title>
		</Dialog.Header>

		<div class="flex flex-col gap-4 py-3">
			{#if activeTab === 'LANGUAGE'}
				<Field.Field class="w-full">
					<Field.Label for="master-item-code">Language Code <span class="text-rose-500">*</span></Field.Label>
					<Field.Content>
						<Input
							id="master-item-code"
							bind:value={itemCodeInput}
							placeholder="e.g., en, hi, mr, gu, ta"
							autocomplete="off"
							class="rounded-xl"
						/>
					</Field.Content>
				</Field.Field>
			{/if}

			{#if activeTab === 'SOURCE_CHANNEL'}
				<!-- ─── Channel Specific Fields ─── -->
				<Field.Field class="w-full">
					<Field.Label for="channel-name">Channel Name <span class="text-rose-500">*</span></Field.Label>
					<Field.Content>
						<Input
							id="channel-name"
							bind:value={itemNameInput}
							placeholder="e.g. Google-Maps, IndiaMART, WhatsApp"
							autocomplete="off"
							class="rounded-xl"
						/>
					</Field.Content>
				</Field.Field>

				<Field.Field class="w-full">
					<Field.Label for="channel-code">Channel Code</Field.Label>
					<Field.Content>
						<Input
							id="channel-code"
							bind:value={itemCodeInput}
							placeholder="e.g. GOOGLE-MAPS, INDIAMART"
							autocomplete="off"
							class="rounded-xl font-mono uppercase"
						/>
					</Field.Content>
				</Field.Field>

				<Field.Field class="w-full">
					<Field.Label for="channel-parent">Parent Lead Source Type</Field.Label>
					<Field.Content>
						<select
							id="channel-parent"
							bind:value={itemParentId}
							class="flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
						>
							<option value={null}>-- None (Independent Channel) --</option>
							{#each availableSources as src}
								<option value={src.id}>{src.name}</option>
							{/each}
						</select>
					</Field.Content>
				</Field.Field>

				<Field.Field class="w-full">
					<Field.Label for="channel-desc">Description</Field.Label>
					<Field.Content>
						<Input
							id="channel-desc"
							bind:value={itemDescriptionInput}
							placeholder="Brief description or usage context..."
							autocomplete="off"
							class="rounded-xl"
						/>
					</Field.Content>
				</Field.Field>

				<Field.Field class="w-full">
					<Field.Content>
						<label class="flex items-center gap-2 text-sm font-medium leading-none cursor-pointer pt-1">
							<input
								type="checkbox"
								bind:checked={itemIsActive}
								class="h-4 w-4 rounded border-input text-primary focus:ring-primary"
							/>
							<span>Active Channel (Available for lead imports & filtering)</span>
						</label>
					</Field.Content>
				</Field.Field>
			{:else if activeTab !== 'CRM_PRODUCTS'}
				<Field.Field class="w-full">
					<Field.Label for="master-item-name">Name <span class="text-rose-500">*</span></Field.Label>
					<Field.Content>
						<Input
							id="master-item-name"
							bind:value={itemNameInput}
							placeholder={activeTab === 'LANGUAGE' ? 'e.g., English, Hindi, Marathi' : `e.g., New ${activeConfig.label.endsWith('s') ? activeConfig.label.slice(0, -1) : activeConfig.label}`}
							autocomplete="off"
							class="rounded-xl"
						/>
					</Field.Content>
				</Field.Field>
			{/if}

			{#if activeTab === 'ACTIVITY_OUTCOME' || activeTab === 'VEHICLE_MAKE' || activeTab === 'VEHICLE_MODEL'}
				<Field.Field class="w-full">
					<Field.Label for="master-item-parent">
						Parent
						{#if activeTab === 'ACTIVITY_OUTCOME'}Activity Type{:else if activeTab === 'VEHICLE_MAKE'}Vehicle Type{:else if activeTab === 'VEHICLE_MODEL'}Vehicle Make{/if}
					</Field.Label>
					<Field.Content>
						<select
							id="master-item-parent"
							bind:value={itemParentId}
							class="flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
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
								<input type="file" accept="image/*" class="hidden" onchange={handleImageUpload} />
							</label>
						{:else}
							<div class="relative h-24 border border-border rounded-xl overflow-hidden bg-card flex items-center justify-center">
								<img src={imageLocalPreview} alt="Preview" class="h-full w-full object-contain p-1" />
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
			{:else if activeTab === 'WHATSAPP_TEMPLATE'}
				<Field.Field class="w-full">
					<Field.Label for="template-language">Language <span class="text-rose-500">*</span></Field.Label>
					<Field.Content>
						<select
							id="template-language"
							bind:value={templateLanguage}
							class="flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
						>
							{#if availableLanguages.length === 0}
								<option value={templateLanguage}>{templateLanguage}</option>
							{:else}
								{#each availableLanguages as lang}
									<option value={lang.name}>{lang.name} ({lang.code})</option>
								{/each}
							{/if}
						</select>
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
								class="flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
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

<!-- ─── Delete Confirmation Modal ─── -->
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
