<script lang="ts">
    import { z } from 'zod';
    import { VenForm, FormGenerator, type FormSchema } from '$lib/components/venUI/form';
    import { Button } from '$lib/components/ui/button';

    // 1. Define Zod Schema
    const paymentSchema = z.object({
        cardName: z.string().min(1, "Please enter the name exactly as it appears on your card."),
        cardNumber: z.string().regex(/^\d{4} \d{4} \d{4} \d{4}$/, "The card number should look like '1234 5678 9012 3456'."),
        cvv: z.string().length(3, "Please enter the 3-digit security code from the back of your card."),
        expiryDate: z.any().optional(), 
        transactionDate: z.any().optional(),
        bookingPeriod: z.any().optional(),
        sameAsShipping: z.boolean().default(true),
        comments: z.string().optional()
    });

    type PaymentData = z.infer<typeof paymentSchema>;

    // 2. Initialize Form Service
    const form = new VenForm<PaymentData>({
        schema: paymentSchema,
        initialValues: {
            sameAsShipping: true
        },
        onSubmit: async (values) => {
            console.log("Form Submitted:", values);
            alert(JSON.stringify(values, null, 2));
        }
    });

    // 3. Define Layout Schema (Matches user example structure)
    const formSchema: FormSchema = [
        {
            type: 'set',
            children: [
                { type: 'legend', text: 'Payment Method' },
                { type: 'description', text: 'All transactions are secure and encrypted' },
                {
                    type: 'group',
                    children: [
                        { 
                            type: 'field', 
                            name: 'cardName', 
                            label: 'Name on Card', 
                            placeholder: 'John Doe',
                            clearable: true
                        },
                        {
                            type: 'grid',
                            cols: 3,
                            children: [
                                { 
                                    type: 'field', 
                                    name: 'cardNumber', 
                                    label: 'Card Number', 
                                    placeholder: '1234 5678 9012 3456',
                                    description: 'Enter your 16-digit number.',
                                    colSpan: 2,
                                    leftIcon: 'credit-card'
                                },
                                { 
                                    type: 'field', 
                                    name: 'cvv', 
                                    label: 'CVV', 
                                    placeholder: '123',
                                    colSpan: 1,
                                    rightIcon: 'lock'
                                }
                            ]
                        },
                        {
                            type: 'grid',
                            cols: 2,
                            children: [
                                {
                                    type: 'field',
                                    name: 'expiryDate',
                                    label: 'Expires (Month Picker)',
                                    inputType: 'date-picker',
                                    picker: 'month',
                                    placeholder: 'Select expiry'
                                },
                                {
                                    type: 'field',
                                    name: 'transactionDate',
                                    label: 'Transaction Date (Time + Presets)',
                                    inputType: 'date-picker',
                                    picker: 'date',
                                    showTime: true,
                                    placeholder: 'Select date & time',
                                    presets: [
                                        { label: 'Today', value: undefined /* Will be handled in component logic to set today */ },
                                        { label: 'Yesterday', value: undefined }
                                        // Note: Real implementation would pass actual DateValue objects here. 
                                        // For demo simplicity, passing empty list or handled inside component default.
                                    ]
                                },
                                {
                                    type: 'field',
                                    name: 'bookingPeriod',
                                    label: 'Booking Range (Range Picker)',
                                    inputType: 'date-picker',
                                    mode: 'range',
                                    placeholder: 'Select range',
                                    colSpan: 2
                                }
                            ]
                        }
                    ]
                }
            ]
        },
        { type: 'separator' },
        {
            type: 'set',
            children: [
                { type: 'legend', text: 'Billing Address' },
                { type: 'description', text: 'The billing address associated with your payment method' },
                {
                    type: 'group',
                    children: [
                        {
                            type: 'field',
                            name: 'sameAsShipping',
                            label: 'Same as shipping address',
                            inputType: 'checkbox'
                        }
                    ]
                }
            ]
        },
        { type: 'separator' },
        {
            type: 'set',
            children: [
                {
                    type: 'group',
                    children: [
                        {
                            type: 'field',
                            name: 'comments',
                            label: 'Comments',
                            inputType: 'textarea',
                            placeholder: 'Add any additional comments'
                        }
                    ]
                }
            ]
        }
    ];
</script>

<div class="p-4 md:p-10 flex flex-col items-center justify-center min-h-[50vh]">
  <div
    class="w-full max-w-md border p-6 rounded-lg shadow-sm bg-card text-card-foreground"
  >
    <h1 class="text-2xl font-bold mb-6">Schema-Driven Form</h1>

    <form
      onsubmit={(e) => {
        console.log("Form Submitted:", form.values);
        e.preventDefault();
        form.submit();
      }}
      class="space-y-6"
    >
      <!-- The Magic Component -->
      <FormGenerator schema={formSchema} {form} root={true} autoFocus={true} />

      <div class="flex items-center gap-4 pt-4">
        <Button type="submit" disabled={form.isSubmitting}>
          {form.isSubmitting ? "Processing..." : "Submit Payment"}
        </Button>
        <Button
          variant="outline"
          type="button"
          onclick={() => (form.values = {} as PaymentData)}
        >
          Reset
        </Button>
      </div>
    </form>
  </div>

  <!-- Debug State -->
  <div class="mt-10 w-full max-w-md p-4 bg-muted/50 rounded text-xs font-mono">
    <p class="font-bold mb-2">Form State:</p>
    <pre>{JSON.stringify(form.values, null, 2)}</pre>
    <p class="font-bold mt-4 mb-2">Errors:</p>
    <pre class="text-destructive">{JSON.stringify(form.errors, null, 2)}</pre>
    <p class="font-bold mb-2">Form IsSubmitting :</p>
    <pre>{JSON.stringify(form.isSubmitting, null, 2)}</pre>
  </div>
</div>
