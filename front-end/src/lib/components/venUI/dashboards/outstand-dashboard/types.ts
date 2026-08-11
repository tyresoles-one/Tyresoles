export interface OutstandingInvoice {
  invoiceNo: string;
  invoiceDate: string;
  dueDate: string;
  product: string;
  totalAmount: number;
  outstandingAmount: number;
  daysOverdue: number;
  status: "Normal" | "Watch" | "High Risk" | "Critical";
}

export interface OutstandingRow {
  id: string;
  region: string;
  dealerCode: string;
  dealerName: string;
  customerCode: string;
  customerName: string;
  respCenter?: string;
  product: string;
  bucket0_30: number;
  bucket31_60: number;
  bucket61_90: number;
  bucket91_180: number;
  bucket181_365: number;
  bucketOver365: number;
  totalBalance: number;
  invoicesCount: number;
  invoices?: OutstandingInvoice[];
}

export type AgingFilterOption =
  | "all"
  | "below30"
  | "below60"
  | "below90"
  | "above30"
  | "above60"
  | "above90"
  | "above180"
  | "above365";

export type GroupingMode = "region-dealer-customer" | "region-dealer-product-customer";

export interface TreeNodeAgg {
  bucket0_30: number;
  bucket31_60: number;
  bucket61_90: number;
  bucket91_180: number;
  bucket181_365: number;
  bucketOver365: number;
  totalBalance: number;
  invoicesCount: number;
  customerCount: number;
}

export interface TreeNode {
  key: string;
  label: string;
  code?: string;
  level: number;
  nodeType: "region" | "dealer" | "product" | "customer";
  agg: TreeNodeAgg;
  children: TreeNode[];
  isLeaf: boolean;
  invoices?: OutstandingInvoice[];
  rawRow?: OutstandingRow;
}

export interface FetchOutstandingParams {
  asOfDate?: string;
  region?: string;
  product?: string;
  respCenters?: string[];
  agingFilter?: AgingFilterOption;
  search?: string;
}
