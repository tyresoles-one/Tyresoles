/**
 * Date formatting and manipulation utilities
 */

/** SQL Server minimum date constant */
export const SQL_MIN_DATE = '1753-01-01';
export const SQL_MIN_DATETIME = '1753-01-01T00:00:00.000Z';

/**
 * Format date for display, treating SQL min date as empty
 * @param dateStr - ISO date string
 * @returns Formatted date (YYYY-MM-DD) or empty string
 */
export function formatDateForDisplay(dateStr: string | null | undefined): string {
	if (!dateStr) return '';
	const date = dateStr.slice(0, 10);
	return date === SQL_MIN_DATE ? '' : date;
}

/**
 * Convert date string to ISO DateTime for GraphQL
 * @param dateStr - Date string (YYYY-MM-DD or ISO)
 * @returns ISO DateTime string
 */
export function toIsoDateTime(dateStr: string): string {
	if (!dateStr) return SQL_MIN_DATETIME;
	if (dateStr.includes('T')) return dateStr;
	return `${dateStr}T00:00:00.000Z`;
}

/**
 * Check if date is SQL min date (empty placeholder)
 * @param dateStr - Date string to check
 * @returns True if date is SQL min date or empty
 */
export function isSqlMinDate(dateStr: string | null | undefined): boolean {
	if (!dateStr) return true;
	return dateStr.startsWith(SQL_MIN_DATE);
}

/**
 * Format date for form input (handles SQL min date)
 * @param dateStr - ISO date string
 * @returns Date string for input or empty
 */
export function formatDateForInput(dateStr: string | null | undefined): string {
	return formatDateForDisplay(dateStr);
}

/**
 * Prepare date for GraphQL mutation (handles empty dates)
 * @param dateStr - Date string from form
 * @returns ISO DateTime or SQL min datetime if empty
 */
export function prepareDateForMutation(dateStr: string | null | undefined): string {
	if (!dateStr || dateStr.trim() === '') {
		return SQL_MIN_DATETIME;
	}
	return toIsoDateTime(dateStr);
}
