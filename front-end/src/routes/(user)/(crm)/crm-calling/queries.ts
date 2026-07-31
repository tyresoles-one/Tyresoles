import { buildQuery, buildMutation } from '$lib/services/graphql';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export type CrmContact = {
	id: string;
	fullName: string;
	companyName?: string | null;
	mobileNo?: string | null;
	city?: string | null;
	state?: string | null;
	respCenter?: string | null;
	erpCustomerNos?: string | null;
	erpAreaCodes?: string | null;
	products?: string | null;
	lastCallDate?: string | null;
};

export type CrmAgentContact = {
	id: string;
	agentUsername: string;
	contactId: string;
	contact: CrmContact;
};

export type GetCrmAgentContactsResult = {
	crmAgentContacts: {
		items: CrmAgentContact[];
		totalCount: number;
	};
};

export type CallLog = {
	id: string;
	contactId: string;
	callDate: string;
	outcome: string;
	notes?: string | null;
	createdBy: string;
};

export type CallReminder = {
	id: string;
	contactId: string;
	reminderDate: string;
	notes?: string | null;
	isCompleted: boolean;
	createdAt: string;
	createdBy: string;
};

export type ContactInvoice = {
	no: string;
	date?: string | null;
	items: string;
	customerName?: string | null;
	qty: number;
	amountToCustomer: number;
};

export type ContactClaim = {
	no: string;
	date?: string | null;
	itemNo: string;
	serialNo: string;
	make: string;
	faultDescription: string;
	decision: string;
	compensationAmount: number;
	mobileNo?: string | null;
};

export type CrmWhatsappImage = {
	id: string;
	name: string;
	imageUrl?: string | null;
	base64Data?: string | null;
	products?: string | null;
	createdAt: string;
};

export type CrmWhatsappTemplate = {
	id: string;
	name: string;
	language: string;
	messageText: string;
	createdAt: string;
};

export type CrmProductItem = {
	id: string;
	code: string;
	category?: string | null;
	productGroup?: string | null;
	finalPrice: number;
	respCenters?: string | null;
	whatsappImageCode?: string | null;
};

export const GetCrmAgentContactsDocument = buildQuery`
	query GetCrmAgentContacts($skip: Int, $take: Int, $where: CrmAgentContactFilterInput, $order: [CrmAgentContactSortInput!]) {
		crmAgentContacts: getCrmAgentContacts(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				id
				agentUsername
				contactId
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					respCenter
					erpCustomerNos
					erpAreaCodes
					products
				}
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<GetCrmAgentContactsResult, { skip?: number; take?: number; where?: any; order?: any }>;

export const GetCrmContactByIdDocument = buildQuery`
	query GetCrmContactById($id: UUID!) {
		contact: getCrmContactById(id: $id) {
			id
			fullName
			companyName
			mobileNo
			city
			state
			respCenter
			erpCustomerNos
			erpAreaCodes
			products
		}
	}
` as unknown as TypedDocumentNode<{ contact: CrmContact | null }, { id: string }>;

export const GetCrmSettingDocument = buildQuery`
	query GetCrmSetting($key: String!) {
		getCrmSetting(key: $key) {
			key
			value
		}
	}
` as unknown as TypedDocumentNode<{ getCrmSetting: { key: string; value: string } | null }, { key: string }>;


export const GetCrmCallLogsDocument = buildQuery`
	query GetCrmCallLogs($contactId: UUID!) {
		crmCallLogs: getCrmCallLogs(contactId: $contactId) {
			id
			contactId
			callDate
			outcome
			notes
			createdBy
		}
	}
` as unknown as TypedDocumentNode<{ crmCallLogs: CallLog[] }, { contactId: string }>;

export const GetCrmCallRemindersDocument = buildQuery`
	query GetCrmCallReminders($contactId: UUID!, $includeCompleted: Boolean!) {
		crmCallReminders: getCrmCallReminders(contactId: $contactId, includeCompleted: $includeCompleted) {
			id
			contactId
			reminderDate
			notes
			isCompleted
			createdAt
			createdBy
		}
	}
` as unknown as TypedDocumentNode<{ crmCallReminders: CallReminder[] }, { contactId: string; includeCompleted: boolean }>;

export const GetCrmContactInvoicesDocument = buildQuery`
	query GetCrmContactInvoices($contactId: UUID!) {
		invoices: getCrmContactInvoices(contactId: $contactId) {
			no
			date
			items
			customerName
			qty
			amountToCustomer
		}
	}
` as unknown as TypedDocumentNode<{ invoices: ContactInvoice[] }, { contactId: string }>;

export const GetCrmContactClaimsDocument = buildQuery`
	query GetCrmContactClaims($contactId: UUID!) {
		claims: getCrmContactClaims(contactId: $contactId) {
			no
			date
			itemNo
			serialNo
			make
			faultDescription
			decision
			compensationAmount
			mobileNo
		}
	}
` as unknown as TypedDocumentNode<{ claims: ContactClaim[] }, { contactId: string }>;


export const GetCrmWhatsappImagesDocument = buildQuery`
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

export const GetCrmWhatsappTemplatesDocument = buildQuery`
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

export const GetCrmCustomerItemPriceDocument = buildQuery`
	query GetCrmCustomerItemPrice($itemNo: String!, $salesCode: String!) {
		price: getCrmCustomerItemPrice(itemNo: $itemNo, salesCode: $salesCode)
	}
` as unknown as TypedDocumentNode<{ price: number | null }, { itemNo: string; salesCode: string }>;

export const GetCrmProductPriceDocument = buildQuery`
	query GetCrmProductPrice($itemNo: String!, $respCenter: String) {
		price: getCrmProductPrice(itemNo: $itemNo, respCenter: $respCenter)
	}
` as unknown as TypedDocumentNode<{ price: number | null }, { itemNo: string; respCenter?: string | null }>;

export const GetCrmProductsDocument = buildQuery`
	query GetCrmProducts($where: CrmProductFilterInput, $order: [CrmProductSortInput!]) {
		products: getCrmProducts(where: $where, order: $order) {
			id
			code
			category
			productGroup
			finalPrice
			respCenters
			whatsappImageCode
		}
	}
` as unknown as TypedDocumentNode<{ products: CrmProductItem[] }, { where?: any; order?: any }>;


export const LogCrmCallDocument = buildMutation`
	mutation LogCrmCall(
		$contactId: UUID!
		$outcome: String!
		$notes: String
		$followUpDate: DateTime
		$followUpNotes: String
		$contactIsActive: Boolean
	) {
		logCrmCall(
			contactId: $contactId
			outcome: $outcome
			notes: $notes
			followUpDate: $followUpDate
			followUpNotes: $followUpNotes
			contactIsActive: $contactIsActive
		) {
			success
			message
		}
	}
` as unknown as TypedDocumentNode<{ logCrmCall: { success: boolean; message: string } }, { contactId: string; outcome: string; notes?: string | null; followUpDate?: string | null; followUpNotes?: string | null; contactIsActive?: boolean | null }>;

export const UndoCrmCallDocument = buildMutation`
	mutation UndoCrmCall($callLogId: UUID!) {
		undoCrmCall(callLogId: $callLogId) {
			success
			message
		}
	}
` as unknown as TypedDocumentNode<{ undoCrmCall: { success: boolean; message: string } }, { callLogId: string }>;

export const GetCrmMyCallingSummaryDocument = buildQuery`
	query GetCrmMyCallingSummary($date: DateTime) {
		summary: getCrmMyCallingSummary(date: $date) {
			outcome
			count
		}
	}
` as unknown as TypedDocumentNode<{ summary: { outcome: string; count: number }[] }, { date?: string }>;

export const CompleteCrmReminderDocument = buildMutation`
	mutation CompleteCrmReminder($reminderId: UUID!) {
		completeCrmReminder(reminderId: $reminderId) {
			success
			message
		}
	}
` as unknown as TypedDocumentNode<{ completeCrmReminder: { success: boolean; message: string } }, { reminderId: string }>;

export const AllocateAgentContactsDocument = buildMutation`
	mutation AllocateAgentContacts($input: AllocateAgentContactsInput) {
		allocateAgentContacts(input: $input) {
			success
			message
			allocatedContacts {
				id
				agentUsername
				contactId
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					respCenter
					erpCustomerNos
					erpAreaCodes
					products
				}
			}
		}
	}
` as unknown as TypedDocumentNode<{ allocateAgentContacts: { success: boolean; message: string; allocatedContacts: CrmAgentContact[] } }, { input?: any }>;

export const DeallocateCrmContactDocument = buildMutation`
	mutation DeallocateCrmContact($contactId: UUID!) {
		deallocateCrmContact(contactId: $contactId) {
			success
			message
		}
	}
` as unknown as TypedDocumentNode<{ deallocateCrmContact: { success: boolean; message: string } }, { contactId: string }>;

export const PrintDocumentsMutation = buildMutation`
	mutation PrintDocuments($input: SalesReportParamsInput!) {
		printDocuments(parameters: $input)
	}
` as unknown as TypedDocumentNode<{ printDocuments: string }, { input: any }>;

export const GetCrmMasterItemsDocument = buildQuery`
	query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
		crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
			id
			name
			parentId
			isPositive
		}
	}
` as unknown as TypedDocumentNode<{ crmMasterItems: { id: number; name: string; parentId: number | null; isPositive: boolean }[] }, { type: string; where?: any }>;

export const GetCrmContactLookupsDocument = buildQuery`
	query GetCrmContactLookups($respCenter: String) {
		getCrmContactLookups(respCenter: $respCenter) {
			states
			cities
			tags
		}
	}
` as unknown as TypedDocumentNode<{ getCrmContactLookups: { states: string[]; cities: string[]; tags: string[] } }, { respCenter?: string }>;

export const GetCrmContactProductsDocument = buildQuery`
	query GetCrmContactProducts($respCenter: String) {
		getCrmContactProducts(respCenter: $respCenter)
	}
` as unknown as TypedDocumentNode<{ getCrmContactProducts: string[] }, { respCenter?: string }>;
