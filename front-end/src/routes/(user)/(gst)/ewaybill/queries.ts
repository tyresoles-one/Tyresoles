import { buildQuery, buildMutation } from '$lib/services/graphql';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export type DCHeaderPosted = {
	no: string;
	date?: string | null;
	documentType: number;
	serRespCenter?: string | null;
	vehicleNo?: string | null;
	vehicleOwner?: string | null;
	responsibilityCenter?: string | null;
	orderNo?: string | null;
	transpGSTIN?: string | null;
	skipEWayBill: number;
	transpDocNo?: string | null;
	transpDocDate?: string | null;
};

export type GetPostedDcHeadersResult = {
	getPostedDcHeaders: {
		items: DCHeaderPosted[];
		totalCount: number;
	};
};

export const GetPostedDcHeadersDocument = buildQuery`
	query GetPostedDcHeaders($skip: Int, $take: Int, $where: DCHeaderPostedFilterInput, $order: [DCHeaderPostedSortInput!]) {
		getPostedDcHeaders(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				no
				date
				documentType
				serRespCenter
				vehicleNo
				vehicleOwner
				responsibilityCenter
				orderNo
				transpGSTIN
				skipEWayBill
				transpDocNo
				transpDocDate
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<GetPostedDcHeadersResult, { skip?: number; take?: number; where?: any; order?: any }>;

export const ProcessPostedDcEWayBillsDocument = buildMutation`
	mutation ProcessPostedDcEWayBills($dcNumbers: [String!]!) {
		processPostedDcEWayBills(dcNumbers: $dcNumbers) {
			success
			message
		}
	}
` as unknown as TypedDocumentNode<{ processPostedDcEWayBills: { success: boolean; message: string } }, { dcNumbers: string[] }>;
