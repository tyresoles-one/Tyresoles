export interface ProcurementRow {
  size: string;
  market: string;
  target: number;
  purchased: number;
  inTransitSql?: number;
  purchasedLastMonth: number;
  avgCost: number;
  avgCostLastMonth: number;
  freight: number;
}
