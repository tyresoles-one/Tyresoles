/**
 * GraphQL Operations Registry
 * 
 * Centralized registry of all GraphQL operations for easy discovery and type-safe access.
 * Operations are imported from generated types after running codegen.
 * 
 * @example
 * ```typescript
 * import { Operations } from '$lib/services/graphql/operations';
 * 
 * const result = await graphqlMutation(Operations.mutations.login, {
 *   variables: { username: 'user', password: 'pass' }
 * });
 * ```
 */

// Import generated document nodes
import { LoginDocument, ChangePasswordDocument, ForgotPasswordDocument } from '../generated/types';

/**
 * Operations registry structure
 * Add new operations here after running codegen
 */
export const Operations = {
	mutations: {
		login: LoginDocument,
		changePassword: ChangePasswordDocument,
		forgotPassword: ForgotPasswordDocument,
	},
	queries: {
		// Add queries here after running codegen
		// Example: getUser: GetUserDocument,
	},
	fragments: {
		// Add fragments here after running codegen
	},
} as const;

/**
 * Type-safe operation name
 */
export type OperationName = 
	| keyof typeof Operations.mutations 
	| keyof typeof Operations.queries;
