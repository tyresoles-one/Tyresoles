import { buildQuery, buildMutation } from '$lib/services/graphql';

export interface CrmWhatsappTemplate {
	id: string;
	name: string;
	language: string;
	languageCode?: string | null;
	category: string;
	status: string;
	headerType: string;
	headerText?: string | null;
	headerMediaUrl?: string | null;
	bodyText?: string | null;
	footerText?: string | null;
	buttonsJson?: string | null;
	qualityScore?: string | null;
	metaTemplateId?: string | null;
	syncedAt?: string | null;
	createdAt: string;
}

export interface CrmWhatsappCampaign {
	id: string;
	name: string;
	templateId?: string | null;
	templateName?: string | null;
	languageCode?: string | null;
	senderPhoneNumberId?: string | null;
	displayPhoneNumber?: string | null;
	status: string;
	scheduledAt?: string | null;
	startedAt?: string | null;
	completedAt?: string | null;
	targetSegmentFilterJson?: string | null;
	variableMappingsJson?: string | null;
	headerMediaUrl?: string | null;
	totalRecipients: number;
	sentCount: number;
	deliveredCount: number;
	readCount: number;
	repliedCount: number;
	failedCount: number;
	costPerMessage: number;
	estimatedCost?: number | null;
	actualCost?: number | null;
	failureReason?: string | null;
	createdAt: string;
	updatedAt?: string | null;
	template?: CrmWhatsappTemplate | null;
}

export interface CrmWhatsappCampaignRecipient {
	id: string;
	campaignId: string;
	contactId?: string | null;
	phoneNumber: string;
	fullName: string;
	companyName?: string | null;
	status: string;
	metaMessageId?: string | null;
	sentAt?: string | null;
	deliveredAt?: string | null;
	readAt?: string | null;
	repliedAt?: string | null;
	replyMessageText?: string | null;
	errorCode?: number | null;
	errorMessage?: string | null;
	createdAt: string;
}

export interface CrmWhatsappSuppression {
	id: string;
	phoneNumber: string;
	reason: string;
	source: string;
	notes?: string | null;
	createdAt: string;
}

export interface WabaHealthStatus {
	isConnected: boolean;
	qualityRating: string;
	messagingLimitTier: string;
	displayPhoneNumber?: string | null;
	verifiedName?: string | null;
	currentDayUsage: number;
	dailyLimit: number;
	statusMessage?: string | null;
}

export interface AudienceEstimateResult {
	totalMatchingContacts: number;
	withValidPhone: number;
	suppressedCount: number;
	previouslyCampaignedCount?: number;
	eligibleRecipients: number;
}

export interface CrmContactWhatsappPickerItem {
	id: string;
	fullName: string;
	companyName?: string | null;
	mobileNo?: string | null;
	mobileNo2?: string | null;
	cleanWhatsappPhone?: string | null;
	city?: string | null;
	state?: string | null;
	contactType?: string | null;
	contactCategory?: string | null;
	respCenter?: string | null;
	qualityScore?: number | null;
	isUniqueNumber?: boolean;
	previousCampaignCount?: number;
}

export interface CrmContactsFilterOptionsResult {
	contactTypes: string[];
	contactCategories: string[];
	respCenters: string[];
	states: string[];
	cities: string[];
}

export interface CrmContactsWhatsappPickerResult {
	items: CrmContactWhatsappPickerItem[];
	totalCount: number;
}

export interface WhatsappGroupTestResult {
	phoneNumber: string;
	success: boolean;
	wamid?: string | null;
	errorCode?: number | null;
	errorMessage?: string | null;
}

export interface CrmWhatsappInboundMessageDto {
	id: string;
	fromPhoneNumber: string;
	profileName?: string | null;
	metaMessageId: string;
	contextWamid?: string | null;
	campaignId?: string | null;
	campaignName?: string | null;
	contactId?: string | null;
	contactFullName?: string | null;
	companyName?: string | null;
	city?: string | null;
	state?: string | null;
	messageType: string;
	messageBody?: string | null;
	buttonPayload?: string | null;
	isProcessed: boolean;
	followupStatus: string;
	receivedAt: string;
}

export interface CrmWhatsappInboundMessagesResult {
	items: CrmWhatsappInboundMessageDto[];
	totalCount: number;
	pendingCount: number;
	contactedCount: number;
	convertedCount: number;
}

export interface AudienceWhatsappFilterInput {
	contactType?: string | null;
	contactCategory?: string | null;
	state?: string | null;
	city?: string | null;
	respCenter?: string | null;
	tag?: string | null;
	minQualityScore?: number | null;
	search?: string | null;
	selectedContactIds?: string[] | null;
	onlyUniqueNumbers?: boolean | null;
}

// QUERIES

export const GET_WHATSAPP_CAMPAIGNS = buildQuery`
	query GetCrmWhatsappCampaigns($status: String, $skip: Int, $take: Int) {
		getCrmWhatsappCampaigns(status: $status, skip: $skip, take: $take) {
			id
			name
			templateId
			templateName
			languageCode
			senderPhoneNumberId
			displayPhoneNumber
			status
			scheduledAt
			startedAt
			completedAt
			totalRecipients
			sentCount
			deliveredCount
			readCount
			repliedCount
			failedCount
			costPerMessage
			estimatedCost
			actualCost
			failureReason
			createdAt
			template {
				id
				name
				category
				status
				headerType
				bodyText
			}
		}
	}
`;

export const GET_WHATSAPP_CAMPAIGN_DETAILS = buildQuery`
	query GetCrmWhatsappCampaignDetails($id: UUID!) {
		getCrmWhatsappCampaignDetails(id: $id) {
			id
			name
			templateId
			templateName
			languageCode
			senderPhoneNumberId
			displayPhoneNumber
			status
			scheduledAt
			startedAt
			completedAt
			targetSegmentFilterJson
			variableMappingsJson
			headerMediaUrl
			totalRecipients
			sentCount
			deliveredCount
			readCount
			repliedCount
			failedCount
			costPerMessage
			estimatedCost
			actualCost
			failureReason
			createdAt
			updatedAt
			template {
				id
				name
				category
				status
				headerType
				headerText
				headerMediaUrl
				bodyText
				footerText
				buttonsJson
			}
		}
	}
`;

export const GET_WHATSAPP_CAMPAIGN_RECIPIENTS = buildQuery`
	query GetCrmWhatsappCampaignRecipients($campaignId: UUID!, $status: String, $skip: Int, $take: Int) {
		getCrmWhatsappCampaignRecipients(campaignId: $campaignId, status: $status, skip: $skip, take: $take) {
			id
			campaignId
			contactId
			phoneNumber
			fullName
			companyName
			status
			metaMessageId
			sentAt
			deliveredAt
			readAt
			repliedAt
			replyMessageText
			errorCode
			errorMessage
			createdAt
		}
	}
`;

export const GET_WHATSAPP_TEMPLATES = buildQuery`
	query GetCrmWhatsappTemplates($category: String, $status: String) {
		getCrmWhatsappTemplates(category: $category, status: $status) {
			id
			name
			language
			languageCode
			category
			status
			headerType
			headerText
			headerMediaUrl
			bodyText
			footerText
			buttonsJson
			qualityScore
			metaTemplateId
			syncedAt
			createdAt
		}
	}
`;

export const GET_WHATSAPP_SUPPRESSION_LIST = buildQuery`
	query GetCrmWhatsappSuppressionList($search: String, $skip: Int, $take: Int) {
		getCrmWhatsappSuppressionList(search: $search, skip: $skip, take: $take) {
			id
			phoneNumber
			reason
			source
			notes
			createdAt
		}
	}
`;

export const ESTIMATE_WHATSAPP_AUDIENCE = buildQuery`
	query EstimateWhatsappAudience($filter: AudienceWhatsappFilterInput) {
		estimateWhatsappCampaignAudience(filter: $filter) {
			totalMatchingContacts
			withValidPhone
			suppressedCount
			previouslyCampaignedCount
			eligibleRecipients
		}
	}
`;

export const GET_WHATSAPP_ACCOUNT_STATUS = buildQuery`
	query GetWhatsappAccountStatus {
		getWhatsappAccountStatus {
			isConnected
			qualityRating
			messagingLimitTier
			displayPhoneNumber
			verifiedName
			currentDayUsage
			dailyLimit
			statusMessage
		}
	}
`;

export const GET_CRM_SETTINGS = buildQuery`
	query GetCrmSettings {
		getCrmSettings {
			key
			value
		}
	}
`;

export const GET_CRM_CONTACTS_FILTER_OPTIONS = buildQuery`
	query GetCrmContactsFilterOptions {
		getCrmContactsFilterOptions {
			contactTypes
			contactCategories
			respCenters
			states
			cities
		}
	}
`;

export const GET_CRM_CONTACTS_FOR_WHATSAPP_PICKER = buildQuery`
	query GetCrmContactsForWhatsappPicker(
		$search: String
		$contactType: String
		$contactCategory: String
		$state: String
		$city: String
		$respCenter: String
		$onlyUniqueNumbers: Boolean
		$skip: Int
		$take: Int
	) {
		getCrmContactsForWhatsappPicker(
			search: $search
			contactType: $contactType
			contactCategory: $contactCategory
			state: $state
			city: $city
			respCenter: $respCenter
			onlyUniqueNumbers: $onlyUniqueNumbers
			skip: $skip
			take: $take
		) {
			totalCount
			items {
				id
				fullName
				companyName
				mobileNo
				mobileNo2
				cleanWhatsappPhone
				city
				state
				contactType
				contactCategory
				respCenter
				qualityScore
				isUniqueNumber
				previousCampaignCount
			}
		}
	}
`;

// MUTATIONS

export const SAVE_WHATSAPP_CAMPAIGN = buildMutation`
	mutation SaveCrmWhatsappCampaign($input: SaveCrmWhatsappCampaignInput!) {
		saveCrmWhatsappCampaign(input: $input) {
			id
			name
			status
		}
	}
`;

export const SCHEDULE_WHATSAPP_CAMPAIGN = buildMutation`
	mutation ScheduleCrmWhatsappCampaign($campaignId: UUID!, $scheduledAt: DateTime) {
		scheduleCrmWhatsappCampaign(campaignId: $campaignId, scheduledAt: $scheduledAt) {
			id
			status
			totalRecipients
			scheduledAt
		}
	}
`;

export const PAUSE_WHATSAPP_CAMPAIGN = buildMutation`
	mutation PauseCrmWhatsappCampaign($campaignId: UUID!) {
		pauseCrmWhatsappCampaign(campaignId: $campaignId) {
			id
			status
		}
	}
`;

export const RESUME_WHATSAPP_CAMPAIGN = buildMutation`
	mutation ResumeCrmWhatsappCampaign($campaignId: UUID!) {
		resumeCrmWhatsappCampaign(campaignId: $campaignId) {
			id
			status
		}
	}
`;

export const CANCEL_WHATSAPP_CAMPAIGN = buildMutation`
	mutation CancelCrmWhatsappCampaign($campaignId: UUID!) {
		cancelCrmWhatsappCampaign(campaignId: $campaignId) {
			id
			status
		}
	}
`;

export const DELETE_WHATSAPP_CAMPAIGN = buildMutation`
	mutation DeleteCrmWhatsappCampaign($campaignId: UUID!) {
		deleteCrmWhatsappCampaign(campaignId: $campaignId)
	}
`;

export const TEST_SEND_WHATSAPP_CAMPAIGN = buildMutation`
	mutation TestSendWhatsappCampaign($campaignId: UUID!, $testPhoneNumber: String!) {
		testSendWhatsappCampaign(campaignId: $campaignId, testPhoneNumber: $testPhoneNumber) {
			success
			wamid
			errorCode
			errorMessage
		}
	}
`;

export const TEST_SEND_WHATSAPP_CAMPAIGN_GROUP = buildMutation`
	mutation TestSendWhatsappCampaignGroup($campaignId: UUID!, $testPhoneNumbers: [String!]!) {
		testSendWhatsappCampaignGroup(campaignId: $campaignId, testPhoneNumbers: $testPhoneNumbers) {
			phoneNumber
			success
			wamid
			errorCode
			errorMessage
		}
	}
`;

export const SYNC_WHATSAPP_TEMPLATES = buildMutation`
	mutation SyncWhatsappTemplates {
		syncWhatsappTemplatesFromMeta {
			id
			name
			status
		}
	}
`;

export const SUBMIT_WHATSAPP_TEMPLATE = buildMutation`
	mutation SubmitWhatsappTemplate($input: CreateTemplateInput!) {
		submitWhatsappTemplateToMeta(input: $input) {
			id
			name
			status
		}
	}
`;

export const ADD_WHATSAPP_SUPPRESSION = buildMutation`
	mutation AddWhatsappSuppression($phoneNumber: String!, $reason: String!, $notes: String) {
		addWhatsappSuppression(phoneNumber: $phoneNumber, reason: $reason, notes: $notes) {
			id
			phoneNumber
		}
	}
`;

export const REMOVE_WHATSAPP_SUPPRESSION = buildMutation`
	mutation RemoveWhatsappSuppression($id: UUID!) {
		removeWhatsappSuppression(id: $id)
	}
`;

export const SAVE_WHATSAPP_SETTINGS = buildMutation`
	mutation SaveWhatsappSettings($input: SaveWhatsappSettingsInput!) {
		saveWhatsappSettings(input: $input)
	}
`;

export const DELETE_WHATSAPP_TEMPLATE = buildMutation`
	mutation DeleteCrmWhatsappTemplate($id: UUID!) {
		deleteCrmWhatsappTemplate(id: $id)
	}
`;

export const GET_WHATSAPP_INBOUND_MESSAGES = buildQuery`
	query GetCrmWhatsappInboundMessages(
		$campaignId: UUID
		$followupStatus: String
		$search: String
		$skip: Int
		$take: Int
	) {
		getCrmWhatsappInboundMessages(
			campaignId: $campaignId
			followupStatus: $followupStatus
			search: $search
			skip: $skip
			take: $take
		) {
			totalCount
			pendingCount
			contactedCount
			convertedCount
			items {
				id
				fromPhoneNumber
				profileName
				metaMessageId
				contextWamid
				campaignId
				campaignName
				contactId
				contactFullName
				companyName
				city
				state
				messageType
				messageBody
				buttonPayload
				isProcessed
				followupStatus
				receivedAt
			}
		}
	}
`;

export const UPDATE_WHATSAPP_INBOUND_FOLLOWUP_STATUS = buildMutation`
	mutation UpdateWhatsappInboundFollowupStatus($id: UUID!, $status: String!) {
		updateWhatsappInboundFollowupStatus(id: $id, status: $status)
	}
`;

export interface CrmWhatsappWebhookLogDto {
	id: string;
	eventType: string;
	fromPhoneNumber?: string | null;
	metaMessageId?: string | null;
	processingStatus: string;
	rawPayload: string;
	signatureHeader?: string | null;
	errorMessage?: string | null;
	campaignId?: string | null;
	campaignName?: string | null;
	recipientId?: string | null;
	receivedAt: string;
}

export interface CrmWhatsappWebhookLogsResult {
	items: CrmWhatsappWebhookLogDto[];
	totalCount: number;
	processedCount: number;
	simulatedCount: number;
	errorCount: number;
}

export interface SimulateWhatsappWebhookInput {
	eventType: string;
	fromPhoneNumber?: string | null;
	customerName?: string | null;
	messageText?: string | null;
	buttonPayload?: string | null;
	metaMessageId?: string | null;
	campaignId?: string | null;
	recipientId?: string | null;
}

export interface SimulateWhatsappWebhookResult {
	success: boolean;
	message: string;
	logId?: string | null;
	inboundMessageId?: string | null;
	metaMessageId?: string | null;
}

export const GET_CRM_WHATSAPP_WEBHOOK_LOGS = buildQuery`
	query GetCrmWhatsappWebhookLogs(
		$eventType: String
		$processingStatus: String
		$skip: Int
		$take: Int
	) {
		getCrmWhatsappWebhookLogs(
			eventType: $eventType
			processingStatus: $processingStatus
			skip: $skip
			take: $take
		) {
			totalCount
			processedCount
			simulatedCount
			errorCount
			items {
				id
				eventType
				fromPhoneNumber
				metaMessageId
				processingStatus
				rawPayload
				signatureHeader
				errorMessage
				campaignId
				campaignName
				recipientId
				receivedAt
			}
		}
	}
`;

export const SIMULATE_WHATSAPP_WEBHOOK = buildMutation`
	mutation SimulateWhatsappWebhook($input: SimulateWhatsappWebhookInput!) {
		simulateWhatsappWebhook(input: $input) {
			success
			message
			logId
			inboundMessageId
			metaMessageId
		}
	}
`;


