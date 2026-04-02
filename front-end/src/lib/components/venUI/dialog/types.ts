export type DialogAction = {
    label: string;
    value: any; // The value to resolve the promise with
    variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
    class?: string;
};

export type DialogOptions = {
    title: string;
    description?: string;
    icon?: string;
    content?: any; // For custom components later
    actions?: DialogAction[];
    cancelLabel?: string; // Default cancel button text if no actions provided
    confirmLabel?: string; // Default confirm button text if no actions provided
    variant?: 'default' | 'destructive' | 'info' | 'success' | 'warning'; // Predefined styles
    
    // Internal use for tracking
    id?: string;
};
