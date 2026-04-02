import { createQuery, createMutation } from '@tanstack/svelte-query';
import { getGraphQLClient } from './client';
import { handleGraphQLError } from './config';
import type { RequestDocument } from 'graphql-request';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import { toast } from '$lib/components/venUI/toast';

/**
 * Extract error message from a raw GraphQL error object.
 * Uses the global handler so toast/onError run once; returns message for rethrowing.
 */
export function extractErrorMessage(error: unknown): string {
	const { message } = handleGraphQLError(error, { silent: true });
	return message || (error instanceof Error ? error.message : 'An unknown error occurred');
}

/**
 * A Svelte Query wrapper for generated GraphQL Queries to eliminate boilerplate
 */
export function useAppQuery<TData, TVariables extends Record<string, unknown> = Record<string, unknown>>(
	document: TypedDocumentNode<TData, TVariables> | RequestDocument,
	variablesFn?: () => TVariables,
	optionsFn?: () => {
        enabled?: boolean;
        queryKey?: any[];
        staleTime?: number;
    }
) {
	return createQuery(() => {
	    const variables = variablesFn ? variablesFn() : undefined;
	    const options = optionsFn ? optionsFn() : undefined;
	    
		const docDef = (document as any).definitions?.[0];
		const queryName = docDef?.name?.value || 'GraphQLQuery';
		const baseKey = options?.queryKey || [queryName, variables];

		return {
			queryKey: baseKey,
			queryFn: async (): Promise<TData> => {
				const client = await getGraphQLClient();
				try {
					return await client.request(document as any, variables as any);
				} catch (error) {
					const { message } = handleGraphQLError(error, { silent: false });
					throw new Error(message || 'An unknown error occurred');
				}
			},
			enabled: options?.enabled,
			staleTime: options?.staleTime,
		};
	});
}

/**
 * A Svelte Query wrapper for generated GraphQL Mutations to eliminate boilerplate
 * Automatically handles success toasts unless silent is true.
 * Global error toast is handled in +layout.svelte MutateCache.
 */
export function useAppMutation<TData, TVariables extends Record<string, any> = Record<string, any>>(
	document: TypedDocumentNode<TData, TVariables> | RequestDocument,
	options?: {
		onSuccess?: (data: TData, variables: TVariables, context: unknown) => void;
		onError?: (error: unknown, variables: TVariables, context: unknown) => void;
		silent?: boolean;
		successMessage?: string | ((data: TData) => string);
	}
) {
	return createMutation<TData, Error, TVariables>(() => ({
		mutationFn: async (variables: TVariables): Promise<TData> => {
			const client = await getGraphQLClient();
			try {
				return await client.request(document as any, variables as any);
			} catch (error) {
				const { message } = handleGraphQLError(error, { silent: false });
				throw new Error(message || 'An unknown error occurred');
			}
		},
		onSuccess: (data: TData, variables: TVariables, context: unknown) => {
			if (!options?.silent) {
				let msg = 'Operation successful';
				if (typeof options?.successMessage === 'function') {
					msg = options.successMessage(data);
				} else if (typeof options?.successMessage === 'string') {
					msg = options.successMessage;
				}
				toast.success(msg);
			}
			options?.onSuccess?.(data, variables, context);
		},
		onError: (error: Error, variables: TVariables, context: unknown) => {
			options?.onError?.(error, variables, context);
		}
	}));
}
