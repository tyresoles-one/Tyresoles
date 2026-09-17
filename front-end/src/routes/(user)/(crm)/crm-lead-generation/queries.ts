import { buildQuery, buildMutation } from '$lib/services/graphql';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export type StagedLeadItem = {
	id: string;
	targetUrl: string;
	sourceUrl: string;
	pageNum: number;
	companyName?: string | null;
	contactPerson?: string | null;
	mobileNo?: string | null;
	altMobileNo?: string | null;
	email?: string | null;
	city?: string | null;
	state?: string | null;
	location?: string | null;
	website?: string | null;
	status: string;
	validationError?: string | null;
	crmContactId?: string | null;
	createdAt?: string | null;
	processedAt?: string | null;
};

export type CrawlQueueSummary = {
	total: number;
	pending: number;
	imported: number;
	duplicate: number;
	rejected: number;
};

export type CrawlCheckpoint = {
	targetUrl: string;
	lastCrawledPage: number;
	nextPageToCrawl: number;
	totalPagesCrawled: number;
	totalListingsDiscovered: number;
	totalPagesDetected?: number | null;
	hasReachedEnd: boolean;
	updatedAt?: string | null;
	queueSummary: CrawlQueueSummary;
};

export type BadgeMetadata = {
	hasBadgeDetected: boolean;
	badgeText?: string | null;
	currentBadgePage?: number | null;
	totalBadgePages?: number | null;
	nextBadgeUrl?: string | null;
	totalItemsCount?: number | null;
};

export type AutoExtractResult = {
	success: boolean;
	message: string;
	targetUrl: string;
	pagesCrawled: number;
	listingsDiscovered: number;
	newLeadsEnqueued: number;
	processedCount: number;
	importedCount: number;
	duplicateCount: number;
	rejectedCount: number;
	badgeMetadata?: BadgeMetadata | null;
	checkpoint?: CrawlCheckpoint | null;
	leads: StagedLeadItem[];
};

export type CrawlPipelineResult = {
	success: boolean;
	message: string;
	pagesCrawled: number;
	listingsDiscovered: number;
	newLeadsEnqueued: number;
	processedCount: number;
	importedCount: number;
	duplicateCount: number;
	rejectedCount: number;
	pendingRemaining: number;
	checkpoint?: CrawlCheckpoint | null;
};

// ─── Auto-Extract Mutation (Single Unified Action with Subprocesses) ───
export const AutoExtractWebLeadsDocument = buildMutation`
	mutation AutoExtractWebLeads(
		$url: String!
		$pages: Int
		$autoIngest: Boolean
		$reset: Boolean
		$division: String
		$targetProduct: String
		$dryRun: Boolean
		$defaultLeadSourceType: String
		$defaultLeadSourceChannel: String
		$defaultRespCenter: String
	) {
		autoExtractWebLeads(
			url: $url
			pages: $pages
			autoIngest: $autoIngest
			reset: $reset
			division: $division
			targetProduct: $targetProduct
			dryRun: $dryRun
			defaultLeadSourceType: $defaultLeadSourceType
			defaultLeadSourceChannel: $defaultLeadSourceChannel
			defaultRespCenter: $defaultRespCenter
		) {
			success
			message
			targetUrl
			pagesCrawled
			listingsDiscovered
			newLeadsEnqueued
			processedCount
			importedCount
			duplicateCount
			rejectedCount
			badgeMetadata {
				hasBadgeDetected
				badgeText
				currentBadgePage
				totalBadgePages
				nextBadgeUrl
				totalItemsCount
			}
			checkpoint {
				targetUrl
				lastCrawledPage
				nextPageToCrawl
				totalPagesCrawled
				totalListingsDiscovered
				totalPagesDetected
				hasReachedEnd
				updatedAt
				queueSummary {
					total
					pending
					imported
					duplicate
					rejected
				}
			}
			leads {
				id
				targetUrl
				sourceUrl
				pageNum
				companyName
				contactPerson
				mobileNo
				altMobileNo
				email
				city
				state
				location
				website
				status
				validationError
				crmContactId
				createdAt
				processedAt
			}
		}
	}
` as unknown as TypedDocumentNode<
	{ autoExtractWebLeads: AutoExtractResult },
	{
		url: string;
		pages?: number | null;
		autoIngest?: boolean | null;
		reset?: boolean | null;
		division?: string | null;
		targetProduct?: string | null;
		dryRun?: boolean | null;
		defaultLeadSourceType?: string | null;
		defaultLeadSourceChannel?: string | null;
		defaultRespCenter?: string | null;
	}
>;

// ─── Query Staged Leads Buffer ───
export const GetStagedLeadsDocument = buildQuery`
	query GetStagedLeads($url: String, $status: String, $limit: Int) {
		stagedLeads: getStagedLeads(url: $url, status: $status, limit: $limit) {
			id
			targetUrl
			sourceUrl
			pageNum
			companyName
			contactPerson
			mobileNo
			altMobileNo
			email
			city
			state
			location
			website
			status
			validationError
			crmContactId
			createdAt
			processedAt
		}
	}
` as unknown as TypedDocumentNode<
	{ stagedLeads: StagedLeadItem[] },
	{ url?: string | null; status?: string | null; limit?: number | null }
>;

// ─── Query Crawl Checkpoint ───
export const GetCrawlCheckpointDocument = buildQuery`
	query GetCrawlCheckpoint($url: String!) {
		checkpoint: getCrawlCheckpoint(url: $url) {
			targetUrl
			lastCrawledPage
			nextPageToCrawl
			totalPagesCrawled
			totalListingsDiscovered
			totalPagesDetected
			hasReachedEnd
			updatedAt
			queueSummary {
				total
				pending
				imported
				duplicate
				rejected
			}
		}
	}
` as unknown as TypedDocumentNode<
	{ checkpoint: CrawlCheckpoint },
	{ url: string }
>;

// ─── Reset Checkpoint Mutation ───
export const ResetCrawlCheckpointDocument = buildMutation`
	mutation ResetCrawlCheckpoint($url: String!, $clearStaging: Boolean) {
		checkpoint: resetCrawlCheckpoint(url: $url, clearStaging: $clearStaging) {
			targetUrl
			lastCrawledPage
			nextPageToCrawl
			totalPagesCrawled
			totalListingsDiscovered
			totalPagesDetected
			hasReachedEnd
			updatedAt
			queueSummary {
				total
				pending
				imported
				duplicate
				rejected
			}
		}
	}
` as unknown as TypedDocumentNode<
	{ checkpoint: CrawlCheckpoint },
	{ url: string; clearStaging?: boolean | null }
>;

// ─── Granular Process 1: Fetch Batch ───
export const FetchWebUrlBatchDocument = buildMutation`
	mutation FetchWebUrlBatch(
		$url: String!
		$pages: Int!
		$reset: Boolean
		$division: String
		$targetProduct: String
	) {
		fetchWebUrlBatch(
			url: $url
			pages: $pages
			reset: $reset
			division: $division
			targetProduct: $targetProduct
		) {
			success
			message
			pagesCrawled
			listingsDiscovered
			newLeadsEnqueued
			checkpoint {
				targetUrl
				lastCrawledPage
				nextPageToCrawl
				totalPagesCrawled
				totalListingsDiscovered
				totalPagesDetected
				hasReachedEnd
				updatedAt
				queueSummary {
					total
					pending
					imported
					duplicate
					rejected
				}
			}
		}
	}
` as unknown as TypedDocumentNode<
	{ fetchWebUrlBatch: CrawlPipelineResult },
	{
		url: string;
		pages: number;
		reset?: boolean | null;
		division?: string | null;
		targetProduct?: string | null;
	}
>;

// ─── Granular Process 2: Ingest Staged Leads ───
export const ProcessStagedLeadsDocument = buildMutation`
	mutation ProcessStagedLeads(
		$url: String
		$limit: Int
		$dryRun: Boolean
		$defaultLeadSourceType: String
		$defaultLeadSourceChannel: String
		$defaultRespCenter: String
	) {
		processStagedLeads(
			url: $url
			limit: $limit
			dryRun: $dryRun
			defaultLeadSourceType: $defaultLeadSourceType
			defaultLeadSourceChannel: $defaultLeadSourceChannel
			defaultRespCenter: $defaultRespCenter
		) {
			success
			message
			processedCount
			importedCount
			duplicateCount
			rejectedCount
			pendingRemaining
			checkpoint {
				targetUrl
				lastCrawledPage
				nextPageToCrawl
				totalPagesCrawled
				totalListingsDiscovered
				hasReachedEnd
				updatedAt
				queueSummary {
					total
					pending
					imported
					duplicate
					rejected
				}
			}
		}
	}
` as unknown as TypedDocumentNode<
	{ processStagedLeads: CrawlPipelineResult },
	{
		url?: string | null;
		limit?: number | null;
		dryRun?: boolean | null;
		defaultLeadSourceType?: string | null;
		defaultLeadSourceChannel?: string | null;
		defaultRespCenter?: string | null;
	}
>;

