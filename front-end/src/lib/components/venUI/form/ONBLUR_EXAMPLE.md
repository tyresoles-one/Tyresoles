# Custom onBlur Handler Example

## How to Use

You can now pass a custom `onBlur` function to any field in your form schema. The handler receives:
1. The current field value
2. The field name

## Example Usage

```typescript
import type { FormSchema } from '$lib/components/venUI/form';

const formSchema: FormSchema = [
  {
    type: 'field',
    name: 'username',
    label: 'Username',
    inputType: 'text',
    placeholder: 'Enter username',
    // Custom blur handler
    onBlur: (value, fieldName) => {
      console.log(`Field ${fieldName} blurred with value:`, value);
      // Your custom logic here
      if (value) {
        // Validate, transform, or trigger side effects
        console.log('Username entered:', value);
      }
    }
  },
  {
    type: 'field',
    name: 'email',
    label: 'Email',
    inputType: 'text',
    placeholder: 'Enter email',
    onBlur: (value, fieldName) => {
      // Validate email format on blur
      if (value && !value.includes('@')) {
        console.warn('Invalid email format');
      }
    }
  }
];
```

## In Your Component

```svelte
<script lang="ts">
  import { VenForm, FormGenerator, type FormSchema } from '$lib/components/venUI/form';
  
  const formSchema: FormSchema = [
    {
      type: 'field',
      name: 'username',
      label: 'Username',
      inputType: 'text',
      onBlur: (value, fieldName) => {
        // Custom validation or side effects
        if (value && value.length < 3) {
          console.warn('Username too short');
        }
      }
    }
  ];
  
  let form = new VenForm({
    schema: loginSchema,
    onSubmit: async (values) => {
      // Handle submit
    }
  });
</script>

<FormGenerator schema={formSchema} {form} root={true} />
```

## What Happens

When a field loses focus:
1. ✅ `form.setTouched(fieldName)` is called (default behavior)
2. ✅ Your custom `onBlur` handler is called (if provided)
3. ✅ Both happen in sequence

## Supported Field Types

The `onBlur` handler works with:
- ✅ Text inputs
- ✅ Textarea
- ✅ Select (called when dropdown closes)
- ✅ Date picker (called on value change)
- ✅ Checkbox (called on checked change)

## Advanced Example

```typescript
const formSchema: FormSchema = [
  {
    type: 'field',
    name: 'phone',
    label: 'Phone Number',
    inputType: 'text',
    onBlur: (value, fieldName) => {
      // Format phone number on blur
      if (value) {
        const formatted = value.replace(/\D/g, ''); // Remove non-digits
        if (formatted.length === 10) {
          // Auto-format: (123) 456-7890
          const formattedPhone = `(${formatted.slice(0,3)}) ${formatted.slice(3,6)}-${formatted.slice(6)}`;
          // Update the form value
          form.setValue(fieldName, formattedPhone);
        }
      }
    }
  }
];
```

## Notes

- The `onBlur` handler is optional - if not provided, only the default `setTouched` behavior occurs
- The handler receives the current value and field name as parameters
- You can access the form instance through closure if needed
- The handler runs after the field is marked as touched
