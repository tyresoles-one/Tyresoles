export type FocusManagerOptions = {
    autoFocus?: boolean;
};

/** Combobox / MasterSelect trigger — used when `indexOf(activeElement)` fails (ref quirks, nested controls). */
const COMBOBOX_TRIGGER = "[data-ven-form-combobox-trigger]";

export function focusManager(node: HTMLElement, options: FocusManagerOptions = {}) {
    /** Skip slot controls (clear, password toggle) — whole field is one tab stop via the primary control. */
    const focusableSelector =
        'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), ' +
        'button:not([disabled]):not([tabindex="-1"]):not([data-ven-form-slot]), ' +
        '[href], [tabindex]:not([tabindex="-1"])';

    function getFocusableElements(): HTMLElement[] {
        return Array.from(node.querySelectorAll(focusableSelector)) as HTMLElement[];
    }

    function resolveFocusIndex(elements: HTMLElement[], current: HTMLElement): number {
        let idx = elements.indexOf(current);
        if (idx !== -1) return idx;
        const anchor = current.closest(COMBOBOX_TRIGGER);
        if (anchor && node.contains(anchor)) {
            idx = elements.indexOf(anchor as HTMLElement);
        }
        return idx;
    }

    function handleKeyDown(e: KeyboardEvent) {
        if (e.key === 'Enter') {
            const target = e.target as HTMLElement;
            // Allow default behavior for buttons (submit) or textareas (newline)
            if (target.tagName === 'BUTTON' || target.tagName === 'TEXTAREA') return;

            e.preventDefault();

            const formEl = target.closest('form');
            if (
                formEl &&
                node.contains(formEl) &&
                (target.tagName === 'INPUT' || target.tagName === 'SELECT')
            ) {
                const submitBtn = formEl.querySelector<HTMLButtonElement>(
                    'button[type="submit"], input[type="submit"]',
                );
                if (submitBtn && !submitBtn.disabled) {
                    const fields = Array.from(
                        formEl.querySelectorAll<
                            HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement
                        >(
                            'input:not([type="hidden"]):not([disabled]), select:not([disabled]), textarea:not([disabled])',
                        ),
                    );
                    const lastField = fields[fields.length - 1];
                    if (lastField && target === lastField) {
                        formEl.requestSubmit(submitBtn);
                        return;
                    }
                }
            }

            focusNext(target);
        } else if (e.key === 'Tab') {
            // Use same filtered focus order as Enter (skip field utility buttons)
            e.preventDefault();
            const target = e.target as HTMLElement;
            const elements = getFocusableElements();
            const index = resolveFocusIndex(elements, target);
            if (e.shiftKey) {
                const prevIndex = index <= 0 ? elements.length - 1 : index - 1;
                elements[prevIndex]?.focus();
            } else {
                const nextIndex = index >= elements.length - 1 ? 0 : index + 1;
                elements[nextIndex]?.focus();
            }
        }
    }

    function focusNext(current: HTMLElement) {
        const elements = getFocusableElements();
        const index = resolveFocusIndex(elements, current);
        const next = index > -1 && index < elements.length - 1 ? elements[index + 1] : null;
        if (next) {
            next.focus();
        }
    }

    // Custom event listener for programmatic focus jumps (e.g. from Select)
    function handleNextFocus(e: Event) {
        const target = e.target as HTMLElement;
        if (!node.contains(target)) return;
        focusNext(target);
    }

    // Initialize Auto Focus
    if (options.autoFocus) {
        // use setTimeout to wait for render
        setTimeout(() => {
            const elements = getFocusableElements();
            if (elements.length > 0) {
                elements[0].focus();
            }
        }, 100);
    }

    node.addEventListener('keydown', handleKeyDown);
    node.addEventListener('ven-form:next-focus', handleNextFocus);

    return {
        update(newOptions: FocusManagerOptions) {
            options = newOptions;
        },
        destroy() {
            node.removeEventListener('keydown', handleKeyDown);
            node.removeEventListener('ven-form:next-focus', handleNextFocus);
        }
    };
}
