# How to Update Form Data Through Events

## Overview

You can update form field values programmatically from event handlers (like `onBlur`) using the `form.setValue()` method.

## Handler Signature

The `onBlur` handler now receives three parameters:

```typescript
onBlur?: (value: any, fieldName: string, form: VenForm<T>) => void;
```

- `value`: Current field value
- `fieldName`: Name of the field
- `form`: Form instance (use this to call `form.setValue()`)

## Available Form Methods

### `form.setValue(fieldName, value)`
Updates a field value programmatically.

```typescript
form.setValue('username', 'newValue');
```

### `form.setTouched(fieldName)`
Marks a field as touched (triggers validation).

```typescript
form.setTouched('username');
```

### `form.validate(fieldName?)`
Validates a specific field or the entire form.

```typescript
form.validate('username'); // Validate single field
form.validate(); // Validate entire form
```

## Examples

### Example 1: Auto-Format on Blur

```typescript
const formSchema: FormSchema = [
  {
    type: 'field',
    name: 'username',
    label: 'Username',
    inputType: 'text',
    onBlur: (value, fieldName, form) => {
      if (value && value.toLowerCase().startsWith('ts:')) {
        // Replace 'ts:' prefix with 'TYRESOLES\'
        const formatted = value.replace(/^ts:/i, 'TYRESOLES\\');
        form.setValue(fieldName, formatted);
      }
    }
  }
];
```

### Example 2: Transform Value

```typescript
{
  type: 'field',
  name: 'email',
  label: 'Email',
  inputType: 'text',
  onBlur: (value, fieldName, form) => {
    if (value) {
      // Convert to lowercase and trim
      const formatted = value.trim().toLowerCase();
      if (formatted !== value) {
        form.setValue(fieldName, formatted);
      }
    }
  }
}
```

### Example 3: Phone Number Formatting

```typescript
{
  type: 'field',
  name: 'phone',
  label: 'Phone Number',
  inputType: 'text',
  onBlur: (value, fieldName, form) => {
    if (value) {
      // Remove all non-digits
      const digits = value.replace(/\D/g, '');
      
      if (digits.length === 10) {
        // Format as (123) 456-7890
        const formatted = `(${digits.slice(0,3)}) ${digits.slice(3,6)}-${digits.slice(6)}`;
        form.setValue(fieldName, formatted);
      }
    }
  }
}
```

### Example 4: Update Multiple Fields

```typescript
{
  type: 'field',
  name: 'fullName',
  label: 'Full Name',
  inputType: 'text',
  onBlur: (value, fieldName, form) => {
    if (value) {
      const parts = value.trim().split(' ');
      if (parts.length >= 2) {
        // Auto-populate first and last name
        form.setValue('firstName', parts[0]);
        form.setValue('lastName', parts.slice(1).join(' '));
      }
    }
  }
}
```

### Example 5: Conditional Updates

```typescript
{
  type: 'field',
  name: 'country',
  label: 'Country',
  inputType: 'select',
  options: [
    { value: 'US', label: 'United States' },
    { value: 'IN', label: 'India' }
  ],
  onBlur: (value, fieldName, form) => {
    // Auto-set phone code based on country
    if (value === 'US') {
      form.setValue('phoneCode', '+1');
    } else if (value === 'IN') {
      form.setValue('phoneCode', '+91');
    }
  }
}
```

### Example 6: Validate and Update

```typescript
{
  type: 'field',
  name: 'website',
  label: 'Website',
  inputType: 'text',
  onBlur: (value, fieldName, form) => {
    if (value && !value.startsWith('http://') && !value.startsWith('https://')) {
      // Auto-add https:// prefix
      form.setValue(fieldName, `https://${value}`);
    }
    
    // Trigger validation after update
    form.validate(fieldName);
  }
}
```

## Current Login Page Example

Your current implementation:

```typescript
{
  type: 'field',
  name: 'username',
  onBlur: (value, fieldName, form) => {
    if(value && value.toLowerCase().startsWith('ts:')) {
      const formattedValue = value.replace(/^ts:/i, 'TYRESOLES\\');
      form.setValue(fieldName, formattedValue); // ✅ Updates the form value
    }
  }
}
```

## Important Notes

1. **Always use `form.setValue()`** - Don't just modify the `value` parameter, it won't update the form
2. **The form instance is reactive** - Changes via `setValue()` will automatically update the UI
3. **Validation runs automatically** - `setTouched()` is called before your handler, which triggers validation
4. **You can call `setValue()` multiple times** - Update the same field or other fields

## Best Practices

1. **Check if value exists** before processing
2. **Compare before updating** to avoid unnecessary re-renders
3. **Use regex for pattern matching** (like `/^ts:/i` for case-insensitive)
4. **Validate after updates** if needed: `form.validate(fieldName)`

## Common Patterns

### Pattern 1: Format and Update
```typescript
onBlur: (value, fieldName, form) => {
  if (value) {
    const formatted = /* transform value */;
    form.setValue(fieldName, formatted);
  }
}
```

### Pattern 2: Conditional Update
```typescript
onBlur: (value, fieldName, form) => {
  if (value && /* condition */) {
    form.setValue(fieldName, /* new value */);
  }
}
```

### Pattern 3: Update Related Fields
```typescript
onBlur: (value, fieldName, form) => {
  if (value) {
    form.setValue('field1', /* value1 */);
    form.setValue('field2', /* value2 */);
  }
}
```
