import { buildQuery, buildMutation } from '$lib/services/graphql';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export type CrmContactInfo = {
	id: string;
	fullName: string;
	companyName?: string | null;
	mobileNo?: string | null;
	city?: string | null;
	state?: string | null;
	respCenter?: string | null;
};

export type DetailedCallLog = {
	id: string;
	contactId: string;
	callDate: string;
	outcome: string;
	notes?: string | null;
	createdBy: string;
	contact?: CrmContactInfo | null;
};

export type GetAllCrmCallLogsResult = {
	crmCallLogs: {
		items: DetailedCallLog[];
		totalCount: number;
	};
};

export type CrmAgentContactInfo = {
	id: string;
	agentUsername: string;
	contactId: string;
	allocatedAt: string;
	deallocatedAt?: string | null;
	lastCallOutcome?: string | null;
	lastCallDate?: string | null;
	callCount: number;
	contact?: CrmContactInfo | null;
};

export type GetCrmAgentContactsResult = {
	crmAgentContacts: {
		items: CrmAgentContactInfo[];
		totalCount: number;
	};
};

export type CrmMasterItem = {
	id: number;
	name: string;
	parentId?: number | null;
	isPositive?: boolean;
};

export type CrmCallReminderInfo = {
	id: string;
	contactId: string;
	reminderDate: string;
	notes?: string | null;
	isCompleted: boolean;
	createdAt: string;
	createdBy: string;
	contact?: CrmContactInfo | null;
};

export type GetAllCrmCallRemindersResult = {
	crmCallReminders: {
		items: CrmCallReminderInfo[];
		totalCount: number;
	};
};

export const GetAllCrmCallLogsDocument = buildQuery`
	query GetAllCrmCallLogs($skip: Int, $take: Int, $where: CrmCallLogFilterInput, $order: [CrmCallLogSortInput!]) {
		crmCallLogs: getAllCrmCallLogs(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				id
				contactId
				callDate
				outcome
				notes
				createdBy
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					state
					respCenter
				}
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<GetAllCrmCallLogsResult, { skip?: number; take?: number; where?: any; order?: any }>;

export const GetCrmAgentContactsDocument = buildQuery`
	query GetCrmAgentContacts($skip: Int, $take: Int, $where: CrmAgentContactFilterInput, $order: [CrmAgentContactSortInput!]) {
		crmAgentContacts: getCrmAgentContacts(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				id
				agentUsername
				contactId
				allocatedAt
				deallocatedAt
				lastCallOutcome
				lastCallDate
				callCount
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					state
					respCenter
				}
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<GetCrmAgentContactsResult, { skip?: number; take?: number; where?: any; order?: any }>;

export const GetCrmMasterItemsDocument = buildQuery`
	query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
		crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
			id
			name
			parentId
			isPositive
		}
	}
` as unknown as TypedDocumentNode<{ crmMasterItems: CrmMasterItem[] }, { type: string; where?: any }>;

export const GetCrmCallLogUsersDocument = buildQuery`
	query GetCrmCallLogUsers {
		users: getCrmCallLogUsers
	}
` as unknown as TypedDocumentNode<{ users: string[] }, {}>;

export const GetAllCrmCallRemindersDocument = buildQuery`
	query GetAllCrmCallReminders($skip: Int, $take: Int, $where: CrmCallReminderFilterInput, $order: [CrmCallReminderSortInput!]) {
		crmCallReminders: getAllCrmCallReminders(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				id
				contactId
				reminderDate
				notes
				isCompleted
				createdAt
				createdBy
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					state
					respCenter
				}
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<GetAllCrmCallRemindersResult, { skip?: number; take?: number; where?: any; order?: any }>;

export const CompleteCrmReminderDocument = buildMutation`
	mutation CompleteCrmReminder($reminderId: UUID!) {
		completeCrmReminder(reminderId: $reminderId) {
			success
			message
		}
	}
` as unknown as TypedDocumentNode<{ completeCrmReminder: { success: boolean; message: string } }, { reminderId: string }>;
