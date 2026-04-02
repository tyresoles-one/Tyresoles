/**
 * Increments the numeric part of an alphanumeric string while preserving leading zeros.
 * e.g. "BEL0001" -> "BEL0002"
 *      "ABC99"   -> "ABC100"
 *      "XYZ"     -> "XYZ1"
 */
export function incrementAlphanumeric(value: string | null | undefined): string {
	if (!value) return "1";
	
	const match = value.match(/^(.*?)(\d+)$/);
	if (!match) {
		return value + "1";
	}
	
	const prefix = match[1];
	const numberStr = match[2];
	const incrementedNumber = (BigInt(numberStr) + 1n).toString();
	
	// Preserve leading zeros if the numeric part was zero-padded
	const paddedNumber = incrementedNumber.padStart(numberStr.length, '0');
	
	return prefix + paddedNumber;
}
