/**
 * GraphQL queries for report-sale master data (myRegions, myCustomers, myAreas, myDealers).
 * Uses buildQuery so it works without running codegen.
 */
import { buildQuery } from '$lib/services/graphql';
import type { RequestDocument } from 'graphql-request';
import {
	GetProductionMakesDocument,
	GetProductionMakeSubMakeDocument,
	GetProductionInspectorCodeNamesDocument,
	GetProductionProcurementInspectionDocument,
	GetProductionProcurementMarketsDocument,
} from '$lib/services/graphql/generated/graphql';

const PAGE_SIZE = 20;

export const GET_MY_REGIONS = buildQuery`
  query GetMyRegions(
    $entityType: String
    $entityCode: String
    $department: String
    $respCenter: String
    $first: Int
    $after: String
    $where: TerritoryFilterInput
  ) {
    myRegions(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenter: $respCenter
      first: $first
      after: $after
      where: $where
    ) {
      nodes { code name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_MY_RESP_CENTERS = buildQuery`
  query GetMyRespCenters(
    $type: String!
    $first: Int
    $after: String
    $where: ResponsibilityCenterFilterInput
  ) {
    myRespCenters(
      type: $type
      first: $first
      after: $after
      where: $where
    ) {
      nodes { code name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;


export const GET_MY_CUSTOMERS = buildQuery`
  query GetMyCustomers(
    $entityType: String
    $entityCode: String
    $department: String
    $respCenter: String
    $first: Int
    $after: String
    $where: CustomerFilterInput
  ) {
    myCustomers(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenter: $respCenter
      first: $first
      after: $after
      where: $where
    ) {
      nodes { no name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_MY_AREAS = buildQuery`
  query GetMyAreas(
    $entityType: String
    $entityCode: String
    $department: String
    $respCenter: String
    $first: Int
    $after: String
    $where: AreaFilterInput
  ) {
    myAreas(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenter: $respCenter
      first: $first
      after: $after
      where: $where
    ) {
      nodes { code name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_MY_DEALERS = buildQuery`
  query GetMyDealers(
    $entityType: String
    $entityCode: String
    $department: String
    $respCenter: String
    $first: Int
    $after: String
    $where: SalespersonPurchaserFilterInput
  ) {
    myDealers(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenter: $respCenter
      first: $first
      after: $after
      where: $where
    ) {
      nodes { code name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_MY_VENDORS = buildQuery`
  query GetMyVendors(
    $respCenter: String
    $categories: [String!]
    $ecoMgr: String
    $after: String
    $first: Int
    $where: VendorFilterInput
  ) {
    myVendors(
      respCenter: $respCenter
      categories: $categories
      ecoMgr: $ecoMgr
      after: $after
      first: $first
      where: $where
    ) {
      nodes { no name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

/** Paged casing / NAV items for ecomile procurement (backend: PurchaseService.ItemNos). */
export const GET_PURCHASE_ITEM_NOS = buildQuery`
  query GetPurchaseItemNos(
    $param: FetchParamsInput!
    $first: Int
    $after: String
    $where: CasingItemFilterInput
  ) {
    purchaseItemNos(param: $param, first: $first, after: $after, where: $where) {
      nodes {
        code
        name
        category
        minRate
        maxRate
      }
      pageInfo {
        hasNextPage
        endCursor
      }
      totalCount
    }
  }
` as RequestDocument;

export const GET_MY_VEHICLES = buildQuery`
  query GetMyVehicles(
    $entityType: String
    $entityCode: String
    $department: String
    $respCenter: String
    $first: Int
    $after: String
    $where: VehiclesFilterInput
  ) {
    myVehicles(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenter: $respCenter
      first: $first
      after: $after
      where: $where
    ) {
      nodes { no name status }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export type MasterType =
  | 'regions'
  | 'customers'
  | 'vendors'
  | 'areas'
  | 'dealers'
  | 'respCenters'
  | 'vehicles'
  | 'purchaseItems'
  /** Backend Query.cs — list<CodeName> (fetch all, filter client-side in MasterSelect). */
  | 'productionMakes'
  | 'productionMakeSubMake'
  | 'productionInspectorCodeNames'
  | 'productionProcurementInspection'
  | 'productionProcurementMarkets';

export interface MasterOption {
  label: string;
  value: string;
}

export function getMasterQuery(masterType: MasterType): RequestDocument {
  switch (masterType) {
    case 'regions':
      return GET_MY_REGIONS;
    case 'customers':
      return GET_MY_CUSTOMERS;
    case 'vendors':
      return GET_MY_VENDORS;
    case 'areas':
      return GET_MY_AREAS;
    case 'dealers':
      return GET_MY_DEALERS;
    case 'respCenters':
      return GET_MY_RESP_CENTERS;
    case 'vehicles':
      return GET_MY_VEHICLES;
    case 'purchaseItems':
      return GET_PURCHASE_ITEM_NOS;
    case 'productionMakes':
      return GetProductionMakesDocument as unknown as RequestDocument;
    case 'productionMakeSubMake':
      return GetProductionMakeSubMakeDocument as unknown as RequestDocument;
    case 'productionInspectorCodeNames':
      return GetProductionInspectorCodeNamesDocument as unknown as RequestDocument;
    case 'productionProcurementInspection':
      return GetProductionProcurementInspectionDocument as unknown as RequestDocument;
    case 'productionProcurementMarkets':
      return GetProductionProcurementMarketsDocument as unknown as RequestDocument;
    default:
      return GET_MY_REGIONS;
  }
}

/** Hot Chocolate returns a full array (no connection paging) for these Query.cs fields. */
export function isProductionArrayMaster(masterType: MasterType): boolean {
  return (
    masterType === 'productionMakes' ||
    masterType === 'productionMakeSubMake' ||
    masterType === 'productionInspectorCodeNames' ||
    masterType === 'productionProcurementInspection' ||
    masterType === 'productionProcurementMarkets'
  );
}

export type ProductionArrayQueryResult = {
  productionMakes?: { code: string; name: string }[];
  productionMakeSubMake?: { code: string; name: string }[];
  productionInspectorCodeNames?: { code: string; name: string }[];
  productionProcurementInspection?: { code: string; name: string }[];
  productionProcurementMarkets?: { code: string; name: string }[];
};

export function getProductionArrayFieldKey(
  masterType: MasterType
): keyof ProductionArrayQueryResult | null {
  switch (masterType) {
    case 'productionMakes':
      return 'productionMakes';
    case 'productionMakeSubMake':
      return 'productionMakeSubMake';
    case 'productionInspectorCodeNames':
      return 'productionInspectorCodeNames';
    case 'productionProcurementInspection':
      return 'productionProcurementInspection';
    case 'productionProcurementMarkets':
      return 'productionProcurementMarkets';
    default:
      return null;
  }
}

/** Field used as value: 'code' for territory/area/dealer/purchaseItems, 'no' for customer/vendor/vehicles. */
export function getValueKey(masterType: MasterType): 'code' | 'no' {
  if (
    masterType === 'productionMakes' ||
    masterType === 'productionMakeSubMake' ||
    masterType === 'productionInspectorCodeNames' ||
    masterType === 'productionProcurementInspection' ||
    masterType === 'productionProcurementMarkets'
  ) {
    return 'code';
  }
  return masterType === 'customers' || masterType === 'vendors' || masterType === 'vehicles'
    ? 'no'
    : 'code';
}

/** Build server-side filter for search (contains on code and name). */
export function buildWhereFilter(
  masterType: MasterType,
  search: string
): Record<string, unknown> | undefined {
  const q = search?.trim();
  if (!q) return undefined;
  const contains = { contains: q };
  if (masterType === 'customers' || masterType === 'vendors' || masterType === 'vehicles') {
    return { or: [{ no: contains }, { name: contains }] };
  }
  if (masterType === 'purchaseItems') {
    return {
      or: [{ code: contains }, { name: contains }, { category: contains }],
    };
  }
  if (isProductionArrayMaster(masterType)) {
    return undefined;
  }
  return { or: [{ code: contains }, { name: contains }] };
}

export { PAGE_SIZE };
