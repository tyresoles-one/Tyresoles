export * from "./models";
export * from "./fetch-params";

/**
 * List of active tyre factories/responsibility centers for ecomile procurement.
 * Used for destination selection in shipments and factory selection in casings.
 */
export const TyreFactoriesActive = [
	{ code: 'BEL', name: 'Belgaum Factory' },
	{ code: 'HOD', name: 'Hubli Factory' },
	{ code: 'MKT', name: 'Market (MKT)' }
];
