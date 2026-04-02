import type { ZodSchema } from 'zod';

export type FieldType = 'text' | 'email' | 'password' | 'number' | 'textarea' | 'checkbox' | 'select' | 'date' | 'date-picker';

export type SelectOption = {
    label: string;
    value: string;
};

// Base props for any node
export interface BaseNode {
    type: string;
    class?: string;
    // Condition to show/hide based on form values (advanced, maybe later)
    // showIf?: (values: any) => boolean; 
}

// Visual/Layout Nodes
export interface GroupNode extends BaseNode {
    type: 'group';
    children: FormNode[];
    orientation?: 'horizontal' | 'vertical'; // For Field.Field wrapper flexibility
}

export interface SetNode extends BaseNode {
    type: 'set';
    children: FormNode[];
}

export interface LegendNode extends BaseNode {
    type: 'legend';
    text: string;
}

export interface DescriptionNode extends BaseNode {
    type: 'description';
    text: string;
}

export interface SeparatorNode extends BaseNode {
    type: 'separator';
}

export interface GridNode extends BaseNode {
    type: 'grid';
    cols?: number | string; // e.g. 2 or '1fr 2fr'
    mobileCols?: number; // Force columns on mobile
    gap?: number;
    children: FormNode[];
}

// Field Nodes
export interface FieldNode extends BaseNode {
    type: 'field';
    name: string; // Key in data object
    required?: boolean;
    label?: string;
    inputType?: FieldType; // Defaults to text
    placeholder?: string;
    description?: string; // Helper text below input
    options?: SelectOption[]; // For select
    // Date Picker specific props
    picker?: 'date' | 'week' | 'month' | 'quarter' | 'year';
    mode?: 'single' | 'range' | 'multiple';
    showTime?: boolean;
    valueFormat?: string; // e.g. "YYYY-MM-DD" (not used by internationalized/date parse but for output)
    displayFormat?: string; 
    presets?: { label: string; value: any }[];
    presetKeys?: string;
    workdate?: any;
    // Specific props
    disabled?: boolean;
    colSpan?: number; // For grid layouts
    orientation?: 'horizontal' | 'vertical' | 'responsive'; // Layout within field
    fieldWidth?: string; // CSS width for input container (e.g. "200px")
    clearable?: boolean; // Show clear button if value exists
    // Adornments
    leftIcon?: string;
    rightIcon?: string;
    // Event handlers
    onBlur?: (value: any, fieldName: string, form: any) => void; // Custom blur handler - receives form instance for setValue()
    // Simple action (e.g. toggle password) - we might need a more robust way to handle actions later
    // For now, let's allow a simple 'toggleVisibility' flag for password fields?
    // Or a generic action handler name?
    // Let's go with generic action handler name that the form instance can resolve?
    // Or just simple click handler prop?
    rightAction?: {
        icon: string;
        label: string;
        onClick: string; // Name of the function in form.actions or similar? 
        // actually simplest is just: type='password' auto-generates toggle?
        // User asked for generic "actions / buttons".
    };
    // For now let's just add icon support, and maybe a specific 'isPassword' flag on FieldNode can be inferred from inputType='password'?
}

// ...

export interface CustomNode extends BaseNode {
    type: 'custom';
    required?: boolean;
    component: any; // Svelte component
    props?: Record<string, any>;
    colSpan?: number;
    orientation?: 'horizontal' | 'vertical' | 'responsive';
}

export type FormNode =
    | GroupNode
    | SetNode
    | LegendNode
    | DescriptionNode
    | SeparatorNode
    | GridNode
    | FieldNode
    | CustomNode;

export type FormSchema = FormNode[];

export interface FormOptions<T> {
    schema?: ZodSchema<T>;
    initialValues?: Partial<T>;
    onSubmit?: (values: T) => void | Promise<void>;
}
