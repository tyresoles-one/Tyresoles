/** One row for the aggregated monthly sales chart. */
export interface MonthlySalesRow {
  month: string;
  sale: number;
  unit?: string;
}

/** Params for fetching sales chart data (aligns with SalesReportParams). */
export interface SalesChartParams {
  from?: string;
  to?: string;
  respCenters?: string[];
  entityType?: string;
  entityCode?: string;
  entityDepartment?: string;
}
