/**
 * Status/State utilities for consistent badge display
 */

export const STATUS_CONFIG = {
	0: { label: 'Active', variant: 'default' as const },
	1: { label: 'Inactive', variant: 'destructive' as const },
	2: { label: 'Pending', variant: 'secondary' as const }
} as const;

export type StatusCode = keyof typeof STATUS_CONFIG;

export interface StatusConfig {
	label: string;
	variant: 'default' | 'destructive' | 'secondary' | 'outline';
}

/**
 * Get status configuration by code
 */
export function getStatusConfig(status: number): StatusConfig {
	return (
		STATUS_CONFIG[status as StatusCode] ?? {
			label: 'Unknown',
			variant: 'default' as const
		}
	);
}

/**
 * Get status badge variant
 */
export function getStatusVariant(status: number): 'default' | 'destructive' | 'secondary' | 'outline' {
	return getStatusConfig(status).variant;
}

/**
 * Get status label text
 */
export function getStatusLabel(status: number): string {
	return getStatusConfig(status).label;
}

/**
 * Alternative labels for specific contexts
 */
export const STATUS_LABELS = {
	ACTIVE_DISABLED: {
		0: 'Active',
		1: 'Disabled'
	},
	ACTIVE_INACTIVE: {
		0: 'Active',
		1: 'Inactive'
	},
	ENABLED_DISABLED: {
		0: 'Enabled',
		1: 'Disabled'
	}
} as const;

/**
 * Get status label with custom context
 */
export function getStatusLabelWithContext(
	status: number,
	context: keyof typeof STATUS_LABELS = 'ACTIVE_INACTIVE'
): string {
	return STATUS_LABELS[context][status as 0 | 1] ?? getStatusConfig(status).label;
}
