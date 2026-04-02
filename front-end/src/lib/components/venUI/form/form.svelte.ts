import { type ZodSchema, ZodError, z } from "zod";
import type { FormOptions } from "./types";

/** Factory for VenForm; accepts zodSchema (alias for schema) for compatibility with report-viewer and other consumers. */
export function CreateForm<T extends Record<string, any>>(
  options: FormOptions<T> & { zodSchema?: ZodSchema<T> },
) {
  const schema = options.zodSchema ?? options.schema;
  return new VenForm<T>({ ...options, schema });
}

// Human-friendly Error Map - Using 'any' to bypass strict Zod type narrowing issues in this setup
const friendlyErrorMap = (issue: any, ctx: any) => {
  // 1. Required Fields
  if (issue.code === z.ZodIssueCode.invalid_type) {
    if (issue.received === "undefined" || issue.received === "null") {
      return { message: "This field is required." };
    }
  }

  // 2. Minimum Length
  if (issue.code === z.ZodIssueCode.too_small) {
    if (issue.type === "string") {
      if (issue.minimum === 1) return { message: "This field is required." };
      return { message: `Please enter at least ${issue.minimum} characters.` };
    }
  }

  // 3. String Formats (Email, URL, etc) - use string for Zod 4 compatibility
  if (issue.code === ("invalid_string" as const)) {
    if (issue.validation === "email")
      return { message: "Please enter a valid email address." };
    if (issue.validation === "url")
      return { message: "Please enter a valid website URL." };
  }

  // Fallback to Zod's default message
  return { message: ctx?.defaultError ?? "Invalid value" };
};

z.setErrorMap(friendlyErrorMap as any);

export class VenForm<T extends Record<string, any>> {
  // Svelte 5 Runes for Reactivity
  values = $state<T>({} as T);
  errors = $state<Record<string, string | undefined>>({});
  touched = $state<Record<string, boolean>>({});
  isSubmitting = $state(false);

  private schema?: ZodSchema<T>;
  private onSubmit?: (values: T) => void | Promise<void>;
  private initialValues: T;

  constructor(options: FormOptions<T>) {
    this.schema = options.schema;
    this.onSubmit = options.onSubmit;
    this.initialValues = (options.initialValues ? { ...options.initialValues } : {}) as T;
    if (options.initialValues) {
      this.values = { ...options.initialValues } as T;
    }
  }

  /** Update the validation schema (e.g. when form fields change dynamically). */
  setSchema(schema: ZodSchema<T>) {
    this.schema = schema;
  }

  /** Restore form values to initial state. */
  reset() {
    this.values = { ...this.initialValues } as T;
    this.errors = {};
    this.touched = {};
  }

  // Validates a single field or whole form
  // Optimized to use safeParse for better performance and error handling
  validate(field?: keyof T): boolean {
    if (!this.schema) return true;

    // Use snapshot to ensure we pass a plain object to Zod
    let valuesToValidate = $state.snapshot(this.values) as Record<string, unknown>;

    // Normalize: Zod "Unrecognized key: 0" occurs when a field is an array (or array-like object with "0"/"1")
    // but schema expects z.object({ start, end }). Convert to { start, end } for date-range fields.
    const toStartEnd = (v: unknown): { start: unknown; end: unknown } | null => {
      if (Array.isArray(v) && v.length >= 2) return { start: v[0], end: v[1] };
      if (v != null && typeof v === "object" && "0" in (v as object) && "1" in (v as object))
        return { start: (v as Record<string, unknown>)["0"], end: (v as Record<string, unknown>)["1"] };
      return null;
    };
    if (valuesToValidate && typeof valuesToValidate === "object" && !Array.isArray(valuesToValidate)) {
      const normalized: Record<string, unknown> = { ...valuesToValidate };
      for (const key of Object.keys(normalized)) {
        const converted = toStartEnd(normalized[key]);
        if (converted) normalized[key] = converted;
      }
      valuesToValidate = normalized;
    }

    // Use safeParse instead of parse for better performance and error handling
    const result = this.schema.safeParse(valuesToValidate);

    if (result.success) {
      // Validation passed - clear errors
      if (field) {
        this.errors[field as string] = undefined;
      } else {
        this.errors = {};
      }
      return true;
    }

    // Validation failed - process errors
    const newErrors: Record<string, string> = {};
    result.error.issues.forEach((issue) => {
      const path = issue.path.join(".");
      newErrors[path] = issue.message;
    });

    if (field) {
      // For single field validation, only update that field's error
      // This prevents showing errors for other fields when validating one
      if (newErrors[field as string]) {
        this.errors[field as string] = newErrors[field as string];
      } else {
        // Field is valid, clear its error
        this.errors[field as string] = undefined;
      }
      // Return true if the specific field is valid, false otherwise
      return !newErrors[field as string];
    } else {
      // For full form validation, update all errors
      this.errors = newErrors;
      return false;
    }
  }

  // Helper for binding
  setValue(field: keyof T, value: any) {
    this.values[field] = value;
    // Optional: validate on change
    // this.validate(field);
  }

  setTouched(field: string) {
    this.touched[field] = true;
    // Validate on blur usually
    this.validate(field as keyof T);
  }

  async submit() {
    this.isSubmitting = true;
    this.touched = Object.keys(this.values).reduce(
      (acc, key) => {
        acc[key] = true;
        return acc;
      },
      {} as Record<string, boolean>,
    );

    const isValid = this.validate();

    if (isValid && this.onSubmit) {
      try {
        await this.onSubmit(this.values);
      } catch (e) {
        console.error("Form submission failed", e);
      }
    }

    this.isSubmitting = false;
  }

  // Helper to get binding props for a field
  // Usage: <Input {...form.control('email')} />
  control(name: keyof T) {
    return {
      name: name as string,
      value: this.values[name],
      oninput: (e: Event) => {
        const target = e.target as HTMLInputElement;
        this.setValue(
          name,
          target.type === "checkbox" ? target.checked : target.value,
        );
      },
      onblur: () => this.setTouched(name),
      "aria-invalid": !!this.errors[name as string],
      "aria-describedby": this.errors[name as string]
        ? `${String(name)}-error`
        : undefined,
    };
  }
}
