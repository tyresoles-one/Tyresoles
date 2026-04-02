import type { DialogOptions, DialogAction } from './types';
import { randomUuidLike } from '$lib/utils/randomId';

type DialogInstance = DialogOptions & {
    id: string;
    resolve: (value: any) => void;
    // We treat 'open' state management here if we want animations to play out nicel 
    // but usually removing from array is enough if component handles unmount.
    // However, for Shadcn Dialog, we need the 'open' prop to be true, then false to trigger close animation, then remove.
    open: boolean;
};

class DialogState {
    dialogs = $state<DialogInstance[]>([]);

    add(options: DialogOptions): Promise<any> {
        return new Promise((resolve) => {
            const id = randomUuidLike();
            this.dialogs.push({
                ...options,
                id,
                resolve,
                open: true
            });
        });
    }

    // Call this when the dialog is actually dismissed/closed
    dismiss(id: string, value: any) {
        const index = this.dialogs.findIndex(d => d.id === id);
        if (index !== -1) {
            const dialog = this.dialogs[index];
            dialog.open = false; // Trigger close animation if possible
            
            // Resolve immediately or wait? 
            // Usually we resolve with the value.
            dialog.resolve(value);

            // Clean up after animation duration (approx 150-300ms) or immediately.
            // For simplicity in this version, we will remove it immediately or rely on onOpenChange in the component.
            // If we remove immediately, animation might be cut.
            // Let's rely on the component to call a 'remove' method after animation closed?
            // Or just minimal timeout.
             setTimeout(() => {
                this.remove(id);
             }, 300);
        }
    }

    remove(id: string) {
        this.dialogs = this.dialogs.filter(d => d.id !== id);
    }
}

const state = new DialogState();

export const dialogService = {
    // Generic open
    open: (options: DialogOptions) => state.add(options),

    // Helpers
    alert: (title: string, description?: string, options?: Partial<DialogOptions>) => {
        return state.add({
            title,
            description,
            confirmLabel: 'OK',
            ...options
        });
    },

    confirm: (title: string, description?: string, options?: Partial<DialogOptions>) => {
        return state.add({
            title,
            description,
            actions: [
                { label: options?.cancelLabel || 'Cancel', value: false, variant: 'outline' },
                { label: options?.confirmLabel || 'Continue', value: true, variant: options?.variant === 'destructive' ? 'destructive' : 'default' }
            ],
            ...options
        });
    },

    // Internal state for the renderer
    get dialogs() {
        return state.dialogs;
    },
    
    // Internal method for the renderer to callback
    _dismiss: (id: string, value: any) => state.dismiss(id, value)
};
