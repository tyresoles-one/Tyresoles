/**
 * Composable for GraphQL mutations with confirmation and error handling
 * 
 * @example
 * ```typescript
 * const updateDealer = useMutation<UpdateDealerMutation, { input: UpdateDealerInput }>({
 *   mutation: UpdateDealerDocument,
 *   successMessage: 'Dealer updated successfully',
 *   confirmTitle: 'Save Changes',
 *   confirmMessage: 'Are you sure you want to save?',
 *   clearCache: (vars) => `dealer-${vars.input.code}`,
 *   onSuccess: async () => { await reload(); }
 * });
 * 
 * // Execute mutation
 * await updateDealer.execute({ input: dealerData });
 * ```
 */

import { graphqlMutation, clearGraphQLCache } from '$lib/services/graphql';
import { Dialog } from '$lib/components/venUI/dialog';
import { toast } from '$lib/components/venUI/toast';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

export interface UseMutationConfig<TMutation, TVariables> {
	/** GraphQL mutation document */
	mutation: TypedDocumentNode<TMutation, TVariables>;
	/** Success toast message */
	successMessage?: string;
	/** Error toast message prefix */
	errorMessage?: string;
	/** Confirmation dialog title (if provided, shows confirmation) */
	confirmTitle?: string;
	/** Confirmation dialog message */
	confirmMessage?: string;
	/** Cache key(s) to clear on success (string, array, or function) */
	clearCache?: string | string[] | ((variables: TVariables) => string | string[]);
	/** Callback on successful mutation */
	onSuccess?: (data: TMutation, variables: TVariables) => void | Promise<void>;
	/** Callback on failed mutation */
	onError?: (error: string, variables: TVariables) => void | Promise<void>;
}

export interface UseMutationReturn<TVariables> {
	/** Execute the mutation */
	execute: (variables: TVariables) => Promise<{ success: boolean; data?: any; error?: string }>;
	/** Saving/loading state */
	saving: boolean;
}

/**
 * Create a mutation handler with confirmation, caching, and error handling
 */
export function useMutation<TMutation, TVariables>(
	config: UseMutationConfig<TMutation, TVariables>
): UseMutationReturn<TVariables> {
	let saving = $state(false);

	async function execute(variables: TVariables) {
		// Optional confirmation dialog
		if (config.confirmTitle) {
			const confirmed = await Dialog.confirm(
				config.confirmTitle,
				config.confirmMessage ?? 'Are you sure you want to proceed?',
				{ confirmLabel: 'Confirm', cancelLabel: 'Cancel' }
			);
			if (!confirmed) {
				return { success: false, error: 'Cancelled by user' };
			}
		}

		saving = true;

		try {
			const result = await graphqlMutation<TMutation>(config.mutation, {
				variables: variables as Record<string, unknown>,
				silent: true
			});

			if (result.success && result.data) {
				// Show success toast
				toast.success(config.successMessage ?? 'Operation completed successfully');

				// Clear cache
				if (config.clearCache) {
					const cacheKeys =
						typeof config.clearCache === 'function'
							? config.clearCache(variables)
							: config.clearCache;
					const keys = Array.isArray(cacheKeys) ? cacheKeys : [cacheKeys];
					keys.forEach((key) => clearGraphQLCache(key));
				}

				// Execute success callback
				await config.onSuccess?.(result.data, variables);

				return { success: true, data: result.data };
			} else {
				// Show error toast
				const errorMsg = result.error ?? 'Operation failed';
				toast.error(config.errorMessage ? `${config.errorMessage}: ${errorMsg}` : errorMsg);

				// Execute error callback
				await config.onError?.(errorMsg, variables);

				return { success: false, error: errorMsg };
			}
		} catch (e) {
			const errorMsg = e instanceof Error ? e.message : 'Operation failed';
			toast.error(config.errorMessage ? `${config.errorMessage}: ${errorMsg}` : errorMsg);

			await config.onError?.(errorMsg, variables);

			return { success: false, error: errorMsg };
		} finally {
			saving = false;
		}
	}

	return {
		execute,
		get saving() {
			return saving;
		}
	};
}
