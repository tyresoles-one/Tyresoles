/** Legacy ecoproc field rule (matches `custom/form` + `InputProps.rules`). */
export type FieldRule = (
	value: unknown,
	data?: Record<string, unknown>
) => string | true | undefined | null;

function isEmpty(value: unknown): boolean {
	if (value === null || value === undefined) return true;
	if (typeof value === "string") return value.trim() === "";
	return false;
}

/** Legacy ecoproc validator: `required('Field label')` → rule for custom `Form`. */
export function required(fieldLabel: string): FieldRule {
	return (value: unknown) => {
		if (!isEmpty(value)) return true;
		return `${fieldLabel} is required`;
	};
}

/** Value must match regex; message defaults to a generic error. */
export function pattern(re: RegExp, message = "Invalid format"): FieldRule {
	return (value: unknown) => {
		if (isEmpty(value)) return true;
		const s = String(value);
		return re.test(s) ? true : message;
	};
}

/** Indian-style 10-digit mobile (digits only). */
export function mobileNo(): FieldRule {
	const re = /^[6-9]\d{9}$/;
	return (value: unknown) => {
		if (isEmpty(value)) return true;
		const s = String(value).replace(/\D/g, "");
		return re.test(s) ? true : "Enter a valid 10-digit mobile number";
	};
}

/** Loose vehicle number (alphanumeric, typical IN format). */
export function vehicleNo(): FieldRule {
	const re = /^[A-Z0-9]{4,12}$/i;
	return (value: unknown) => {
		if (isEmpty(value)) return true;
		const s = String(value).replace(/\s+/g, "");
		return re.test(s) ? true : "Enter a valid vehicle number";
	};
}
