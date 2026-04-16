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
    $respCenters: [String!]
    $first: Int
    $after: String
    $where: TerritoryFilterInput
  ) {
    myRegions(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenters: $respCenters
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
    $respCenters: [String!]
    $first: Int
    $after: String
    $where: AreaFilterInput
  ) {
    myAreas(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenters: $respCenters
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
    $respCenters: [String!]
    $first: Int
    $after: String
    $where: SalespersonPurchaserFilterInput
  ) {
    myDealers(
      entityType: $entityType
      entityCode: $entityCode
      department: $department
      respCenters: $respCenters
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

/** NAV Post Code (PIN + city/state). Backend: Query.GetPostCodes / CommonDataService.GetPostCodesQuery. */
export const GET_POST_CODES = buildQuery`
  query GetPostCodesMaster(
    $first: Int
    $after: String
    $where: PostCodeFilterInput
    $order: [PostCodeSortInput!]
  ) {
    postCodes(first: $first, after: $after, where: $where, order: $order) {
      nodes {
        code
        city
        searchCity
        stateCode
        county
        countryRegionCode
      }
      pageInfo {
        hasNextPage
        endCursor
      }
      totalCount
    }
  }
` as RequestDocument;

/** NAV State (code + description). Backend: Query.GetStates / CommonDataService.GetStatesQuery. */
export const GET_STATES = buildQuery`
  query GetStatesMaster(
    $first: Int
    $after: String
    $where: StateFilterInput
    $order: [StateSortInput!]
  ) {
    states(first: $first, after: $after, where: $where, order: $order) {
      nodes {
        code
        description
      }
      pageInfo {
        hasNextPage
        endCursor
      }
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

export const GET_GL_ACCOUNTS = buildQuery`
  query GetGLAccounts(
    $first: Int
    $after: String
    $where: GLAccountFilterInput
    $order: [GLAccountSortInput!]
  ) {
    glAccounts(first: $first, after: $after, where: $where, order: $order) {
      nodes { no name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_PAYROLL_EMPLOYEES = buildQuery`
  query GetPayrollEmployees(
    $param: ReportFetchParamInput!
    $first: Int
    $after: String
    $where: EmployeeFilterInput
  ) {
    payrollEmployees(param: $param, first: $first, after: $after, where: $where) {
      nodes { no firstName lastName }
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

/** Group Category rows (e.g. departments when type = 1). Backend: Query.GetGroupCategories → GraphQL `groupCategories` (same pattern as `GetGroupDetails` → `groupDetails`). */
export const GET_GROUP_CATEGORIES = buildQuery`
  query GetGroupCategories($type: Int!, $respCenters: String) {
    groupCategories(type: $type, respCenters: $respCenters) {
      code
      name
      type
      status
      responsibilityCenter
    }
  }
` as RequestDocument;

/** NAV User (User Setup) — Query.searchUsers → IUserService.SearchUsersAsync. */
export const GET_SEARCH_USERS = buildQuery`
  query SearchUsersMaster($search: String, $take: Int) {
    searchUsers(search: $search, take: $take) {
      userId
      fullName
      userType
      avatar
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

export const GET_UNIT_OF_MEASURE = buildQuery`
  query GetUnitOfMeasures(
    $first: Int
    $after: String
    $where: UnitOfMeasureFilterInput
    $order: [UnitOfMeasureSortInput!]
  ) {
    unitOfMeasures(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_ITEM_CATEGORY = buildQuery`
  query GetItemCategories(
    $first: Int
    $after: String
    $where: ItemCategoryFilterInput
    $order: [ItemCategorySortInput!]
  ) {
    itemCategories(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_PRODUCT_GROUP = buildQuery`
  query GetProductGroups(
    $first: Int
    $after: String
    $where: ProductGroupFilterInput
    $order: [ProductGroupSortInput!]
  ) {
    productGroups(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description itemCategoryCode }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_GEN_PRODUCT_POSTING_GROUP = buildQuery`
  query GetGenProductPostingGroups(
    $first: Int
    $after: String
    $where: GenProductPostingGroupFilterInput
    $order: [GenProductPostingGroupSortInput!]
  ) {
    genProductPostingGroups(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_GST_GROUP = buildQuery`
  query GetGSTGroups(
    $first: Int
    $after: String
    $where: GSTGroupFilterInput
    $order: [GSTGroupSortInput!]
  ) {
    gstGroups(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_HSN_SAC = buildQuery`
  query GetHsnSacs(
    $first: Int
    $after: String
    $where: HsnSacFilterInput
    $order: [HsnSacSortInput!]
  ) {
    hsnSacs(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_INVENTORY_POSTING_GROUPS = buildQuery`
  query GetInventoryPostingGroups(
    $first: Int
    $after: String
    $where: InventoryPostingGroupFilterInput
    $order: [InventoryPostingGroupSortInput!]
  ) {
    inventoryPostingGroups(first: $first, after: $after, where: $where, order: $order) {
      nodes { code description }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_ITEMS = buildQuery`
  query GetItems(
    $first: Int
    $after: String
    $where: ItemFilterInput
    $order: [ItemSortInput!]
  ) {
    items(first: $first, after: $after, where: $where, order: $order) {
      nodes { no description baseUnitOfMeasure itemCategoryCode productGroupCode inventoryPostingGroup genProdPostingGroup gstGroupCode hsnSacCode }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_FA_CLASSES = buildQuery`
  query GetFAClasses(
    $first: Int
    $after: String
    $where: FAClassFilterInput
    $order: [FAClassSortInput!]
  ) {
    faClasses(first: $first, after: $after, where: $where, order: $order) {
      nodes { code name }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_FA_SUBCLASSES = buildQuery`
  query GetFASubclasses(
    $first: Int
    $after: String
    $where: FASubclassFilterInput
    $order: [FASubclassSortInput!]
  ) {
    faSubclasses(first: $first, after: $after, where: $where, order: $order) {
      nodes { code name faClassCode }
      pageInfo { hasNextPage endCursor }
      totalCount
    }
  }
` as RequestDocument;

export const GET_FIXED_ASSETS = buildQuery`
  query GetFixedAssets(
    $first: Int
    $after: String
    $where: FixedAssetFilterInput
    $order: [FixedAssetSortInput!]
  ) {
    fixedAssets(first: $first, after: $after, where: $where, order: $order) {
      nodes { no description description2 serialNo vendorNo faClassCode faSubclassCode responsibilityCenter responsibleEmployee }
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
  /** NAV Post Code — Query.postCodes (paged, filtered, sorted). */
  | 'postCodes'
  /** NAV State — Query.states (paged, filtered, sorted). */
  | 'states'
  /** Backend Query.cs — list<CodeName> (fetch all, filter client-side in MasterSelect). */
  | 'productionMakes'
  | 'productionMakeSubMake'
  | 'productionInspectorCodeNames'
  | 'productionProcurementInspection'
  | 'productionProcurementMarkets'
  /** NAV Group Category — Query.GetGroupCategories (type + optional comma-separated resp centers). */
  | 'departments'
  /** NAV Employee — Query.payrollEmployees (paged, filtered, sorted). */
  | 'payrollEmployees'
  /** NAV User (login / User Setup) — Query.searchUsers (search + take, no cursor). */
  | 'users'
  /** NAV G/L Account — Query.glAccounts (paged, filtered, sorted). */
  | 'glAccounts'
  | 'unitOfMeasures'
  | 'itemCategories'
  | 'productGroups'
  | 'genProductPostingGroups'
  | 'gstGroups'
  | 'hsnSacs'
  | 'inventoryPostingGroups'
  | 'items'
  | 'faClasses'
  | 'faSubclasses'
  | 'fixedAssets';

export interface MasterOption {
  label: string;
  value: string;
  /** Optional row payload (e.g. NAV Post Code city/state when masterType is postCodes). */
  meta?: Record<string, unknown>;
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
    case 'postCodes':
      return GET_POST_CODES;
    case 'states':
      return GET_STATES;
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
    case 'departments':
      return GET_GROUP_CATEGORIES;
    case 'payrollEmployees':
      return GET_PAYROLL_EMPLOYEES;
    case 'users':
      return GET_SEARCH_USERS;
    case 'glAccounts':
      return GET_GL_ACCOUNTS;
    case 'unitOfMeasures':
      return GET_UNIT_OF_MEASURE;
    case 'itemCategories':
      return GET_ITEM_CATEGORY;
    case 'productGroups':
      return GET_PRODUCT_GROUP;
    case 'genProductPostingGroups':
      return GET_GEN_PRODUCT_POSTING_GROUP;
    case 'gstGroups':
      return GET_GST_GROUP;
    case 'hsnSacs':
      return GET_HSN_SAC;
    case 'inventoryPostingGroups':
      return GET_INVENTORY_POSTING_GROUPS;
    case 'items':
      return GET_ITEMS;
    case 'faClasses':
      return GET_FA_CLASSES;
    case 'faSubclasses':
      return GET_FA_SUBCLASSES;
    case 'fixedAssets':
      return GET_FIXED_ASSETS;
    default:
      return GET_MY_REGIONS;
  }
}

/** Uses Query.searchUsers — refetch when search text changes (no connection paging). */
export function isSearchUsersMaster(masterType: MasterType): boolean {
  return masterType === 'users';
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

export function isGroupCategoriesMaster(masterType: MasterType): boolean {
  return masterType === 'departments';
}

export type ProductionArrayQueryResult = {
  productionMakes?: { code: string; name: string }[];
  productionMakeSubMake?: { code: string; name: string }[];
  productionInspectorCodeNames?: { code: string; name: string }[];
  productionProcurementInspection?: { code: string; name: string }[];
  productionProcurementMarkets?: { code: string; name: string }[];
};

export type GroupCategoriesQueryResult = {
  groupCategories?: { code: string; name: string }[];
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
    masterType === 'productionProcurementMarkets' ||
    masterType === 'postCodes' ||
    masterType === 'states'
  ) {
    return 'code';
  }
  return masterType === 'customers' ||
    masterType === 'vendors' ||
    masterType === 'vehicles' ||
    masterType === 'payrollEmployees' ||
    masterType === 'glAccounts' ||
    masterType === 'items' ||
    masterType === 'fixedAssets'
    ? 'no'
    : 'code';
}

/** Build server-side filter for search (contains on fields that exist on each *FilterInput). */
export function buildWhereFilter(
  masterType: MasterType,
  search: string
): Record<string, unknown> | undefined {
  const q = search?.trim();
  if (!q) return undefined;
  const contains = { contains: q };

  switch (masterType) {
    case 'payrollEmployees':
      return {
        or: [{ no: contains }, { firstName: contains }, { lastName: contains }, { searchName: contains }],
      };
    case 'customers':
    case 'vendors':
      return {
        or: [
          { no: contains },
          { name: contains },
          { searchName: contains },
          { name2: contains },
          { contact: contains },
          { phoneNo: contains },
          { panNo: contains },
          { eMail: contains },
        ],
      };
    case 'vehicles':
      return { or: [{ no: contains }, { name: contains }, { mobileNo: contains }, { gstNo: contains }] };
    case 'glAccounts':
      return { or: [{ no: contains }, { name: contains }] };
    case 'items':
      return { or: [{ no: contains }, { description: contains }] };
    case 'fixedAssets':
      return { or: [{ no: contains }, { description: contains }, { description2: contains }] };
    case 'faClasses':
      return { or: [{ code: contains }, { name: contains }] };
    case 'faSubclasses':
      return { or: [{ code: contains }, { name: contains }, { faClassCode: contains }] };
    case 'unitOfMeasures':
    case 'itemCategories':
    case 'productGroups':
    case 'genProductPostingGroups':
    case 'gstGroups':
    case 'hsnSacs':
    case 'inventoryPostingGroups':
      return { or: [{ code: contains }, { description: contains }] };
    default:
      break;
  }

  if (masterType === 'purchaseItems') {
    return {
      or: [{ code: contains }, { name: contains }, { category: contains }],
    };
  }
  if (masterType === 'postCodes') {
    return {
      or: [
        { code: contains },
        { city: contains },
        { searchCity: contains },
        { stateCode: contains },
        { county: contains },
      ],
    };
  }
  if (masterType === 'states') {
    return {
      or: [{ code: contains }, { description: contains }],
    };
  }
  if (isProductionArrayMaster(masterType) || isGroupCategoriesMaster(masterType)) {
    return undefined;
  }
  return { or: [{ code: contains }, { name: contains }] };
}

export { PAGE_SIZE };
