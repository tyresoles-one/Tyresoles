/** Matches backend ClaimRatio JSON (camelCase from System.Text.Json). */
export interface ClaimRatioRow {
  companyName?: string;
  reportName?: string;
  locations?: string;
  period?: string;
  view?: string;
  particular?: string;
  group?: string;
  particularLbl?: string;
  bValue?: boolean;
  sold?: number;
  purchase?: number;
  inTransit?: number;
  inTransitSql?: number;
  claims?: number;
  pass?: number;
  reject?: number;
  unsettled?: number;
  specialCase?: number;
  claimPercent?: number;
  passPercent?: number;
  saleValue?: number;
  creditNoteValue?: number;
  creditNotePercent?: number;
  level01?: string;
  level02?: string;
  level03?: string;
  level04?: string;
  respCenter?: string;
}


/** Views accepted by DashboardController and ProductionReportService Claim Ratios SQL. */
export const CLAIM_RATIOS_VIEWS = [
  "Product wise",
  "Pattern wise",
  "Make wise",
  "Submake wise",
  "Dealer wise",
  "Salesperson wise",
  "Defect wise",
  "Proc. Market wise",
] as const;

export type ClaimRatiosView = (typeof CLAIM_RATIOS_VIEWS)[number];
