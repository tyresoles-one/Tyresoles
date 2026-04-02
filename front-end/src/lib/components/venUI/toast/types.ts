export type ToastAction = {
    label: string;
    onClick: () => void;
};

export type ToastType = 'default' | 'success' | 'error' | 'warning' | 'info';

export type ToastOptions = {
    id?: string | number;
    title?: string; // Optional if you just want description
    description: string;
    type?: ToastType;
    action?: ToastAction;
    cancel?: {
        label: string;
        onClick?: () => void;
    };
    duration?: number;
    icon?: string; // Custom icon name from venUI/icon
};
