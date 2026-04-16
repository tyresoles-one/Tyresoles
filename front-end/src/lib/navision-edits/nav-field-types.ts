import type { MasterType } from "$lib/components/venUI/master-select/masters-api";

/** Built-in input kinds (no master lookup). */
export const NAV_EDIT_PRIMITIVE_FIELD_TYPES = ["text", "date", "select", "number"] as const;

/**
 * Master types allowed on the Navision Edits user form (MasterSelect works with auth context only).
 * Excludes purchase/production masters that require extra GraphQL params.
 */
export const NAV_EDIT_MASTER_FIELD_TYPES: readonly MasterType[] = [
  "regions",
  "customers",
  "vendors",
  "areas",
  "dealers",
  "respCenters",
  "vehicles",
  "postCodes",
  "states",
  "departments",
  "payrollEmployees",
  "users",
  "glAccounts",
  "unitOfMeasures",
  "itemCategories",
  "productGroups",
  "genProductPostingGroups",
  "gstGroups",
  "hsnSacs",
  "inventoryPostingGroups",
  "items",
  "faClasses",
  "faSubclasses",
  "fixedAssets",
] as const;

const MASTER_LABELS: Partial<Record<MasterType, string>> = {
  regions: "Regions",
  customers: "Customers",
  vendors: "Vendors",
  areas: "Areas",
  dealers: "Dealers",
  respCenters: "Responsibility centers",
  vehicles: "Vehicles",
  purchaseItems: "Purchase / casing items",
  postCodes: "Post codes",
  states: "States",
  productionMakes: "Production makes",
  productionMakeSubMake: "Production make / sub-make",
  productionInspectorCodeNames: "Production inspectors",
  productionProcurementInspection: "Procurement inspection",
  productionProcurementMarkets: "Procurement markets",
  departments: "Departments / group categories",
  payrollEmployees: "Payroll employees",
  users: "Nav users",
  glAccounts: "G/L Account",
  unitOfMeasures: "Unit of Measure",
  itemCategories: "Item Category",
  productGroups: "Product Group",
  genProductPostingGroups: "Gen. Product Posting Group",
  gstGroups: "GST Group",
  hsnSacs: "HSN/SAC",
  inventoryPostingGroups: "Inventory Posting Group",
  items: "Item",
  faClasses: "Fixed Asset Class",
  faSubclasses: "Fixed Asset Subclass",
  fixedAssets: "Fixed Asset",
};

export function navEditFieldTypeSelectOptions(): { value: string; label: string }[] {
  const primitives = NAV_EDIT_PRIMITIVE_FIELD_TYPES.map(t => ({ value: t, label: t }));
  const masters = NAV_EDIT_MASTER_FIELD_TYPES.map(m => ({
    value: `master:${m}`,
    label: `Master: ${MASTER_LABELS[m] ?? m}`,
  }));
  return [...primitives, ...masters];
}

/** If `field.type` is `master:<MasterType>`, return the master key; otherwise null. */
export function parseNavEditFieldMasterType(fieldType: string): MasterType | null {
  if (!fieldType.startsWith("master:")) return null;
  const key = fieldType.slice(7);
  return (NAV_EDIT_MASTER_FIELD_TYPES as readonly string[]).includes(key)
    ? (key as MasterType)
    : null;
}

/** Merge known options with a legacy/unknown `type` string so the designer still shows it. */
export function navEditFieldTypeOptionsWith(current: string): { value: string; label: string }[] {
  const base = navEditFieldTypeSelectOptions();
  const t = current?.trim();
  if (!t) return base;
  if (base.some(o => o.value === t)) return base;
  return [...base, { value: t, label: t }];
}
