import { toast } from 'svelte-sonner';
import ToastView from './toast-view.svelte';
import type { ToastOptions, ToastType } from './types';

// Helper to show the toast
function show(options: ToastOptions) {
    const id = options.id ?? Date.now() + Math.random();
    return toast.custom(ToastView, {
        id,
        componentProps: {
            id,
            options
        },
        duration: options.duration
    });
}

export const Toast = {
    // Basic types
    success: (title: string, description?: string, options?: Partial<ToastOptions>) => {
        show({ 
            ...options,
            type: 'success', 
            description: description ?? options?.description ?? title,
            title: description ? title : undefined
        });
    },
    error: (title: string, description?: string, options?: Partial<ToastOptions>) => {
        show({ 
            ...options,
            type: 'error', 
            description: description ?? options?.description ?? title,
            title: description ? title : undefined
        });
    },
    warning: (title: string, description?: string, options?: Partial<ToastOptions>) => {
        show({ 
            ...options,
            type: 'warning', 
            description: description ?? options?.description ?? title,
            title: description ? title : undefined
        });
    },
    info: (title: string, description?: string, options?: Partial<ToastOptions>) => {
        show({ 
            ...options,
            type: 'info', 
            description: description ?? options?.description ?? title,
            title: description ? title : undefined
        });
    },
    // Generic
    show: (options: ToastOptions) => show(options),
    
    // Dismiss
    dismiss: (id: string | number) => toast.dismiss(id)
};
