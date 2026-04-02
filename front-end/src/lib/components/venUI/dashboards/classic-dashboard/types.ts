/** Dashboard request params aligned with backend SalesReportParams and reportsale ReportFetchParams. */
export interface FetchParams {
  from: string;
  to: string;
  reportName: string;
  respCenters?: string[];
  customers?: string[];
  dealers?: string[];
  areas?: string[];
  regions?: string[];
  /** Maps to backend SalesReportParams.EntityType (e.g. Customer, Partner, Employee). */
  entityType?: string;
  /** Maps to backend SalesReportParams.EntityCode. */
  entityCode?: string;
  /** Maps to backend SalesReportParams.EntityDepartment (e.g. Sales). */
  entityDepartment?: string;
}

export interface DashboardData {
  name: string;
  data: any[];
}

export interface Item {
  label: string;
  value: number;
  unit: string;
}

export interface Sales {
  label: string;
  dateRange: string;
  sale: number;
  saleText: string;
  saleUnit: string;
  items: Item[];
}

export interface ProductSale {
  business: string;
  location: string;
  product: string;
  data: Sales[];
}

export interface SaleLine {
  description: string;
  amount: number;
  unit: string;
}

export interface CustomerSaleBalance {
  code: string;
  name: string;
  sale: number;
}

export interface CustomerSales {
  dateRange: string;
  records: CustomerSaleBalance[];
  lines: SaleLine[];
}

export interface ActiveCustomer {
  business: string;
  location: string;
  product: string;
  data: CustomerSales[];
}

export interface CollectionRecord {
  [key: string]: any;
}

export interface CollectionData {
  business: string;
  location: string;
  period: string;
  collection: number;
  data: CollectionRecord[];
}

export interface SalesmanSale {
  business: string;
  location: string;
  product: string;
  data: Record<string, any>[];
}

export interface BusinessLocation {
  business: string;
  locations: string[];
  selections: string[];
  default: string;
}

export type ViewName =
  | "Product Sale"
  | "Collection"
  | "Active Customers"
  | "Salesperson"
  | "Dealer";

export type ViewValue =
  | "ProductSale"
  | "Collection"
  | "ActiveCustomer"
  | "SalesmanSale"
  | "DealerSale";

export const VIEWS: { label: ViewName; value: ViewValue; icon: string }[] = [
  { label: "Product Sale", value: "ProductSale", icon: "package" },
  { label: "Collection", value: "Collection", icon: "banknote" },
  { label: "Active Customers", value: "ActiveCustomer", icon: "users" },
  { label: "Salesperson", value: "SalesmanSale", icon: "user-check" },
  { label: "Dealer", value: "DealerSale", icon: "store" },
];
