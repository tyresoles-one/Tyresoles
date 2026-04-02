import type { RequestDocument } from 'graphql-request';

/**
 * Build a GraphQL query string with type safety
 * 
 * @example
 * ```ts
 * const query = buildQuery`
 *   query GetUser($id: ID!) {
 *     user(id: $id) {
 *       id
 *       name
 *       email
 *     }
 *   }
 * `;
 * ```
 */
export function buildQuery(
	strings: TemplateStringsArray,
	...values: unknown[]
): RequestDocument {
	return strings.reduce((acc, str, i) => {
		return acc + str + (values[i] ?? '');
	}, '');
}

/**
 * Build a GraphQL mutation string with type safety
 * 
 * @example
 * ```ts
 * const mutation = buildMutation`
 *   mutation UpdateUser($id: ID!, $input: UserInput!) {
 *     updateUser(id: $id, input: $input) {
 *       id
 *       name
 *       email
 *     }
 *   }
 * `;
 * ```
 */
export function buildMutation(
	strings: TemplateStringsArray,
	...values: unknown[]
): RequestDocument {
	return strings.reduce((acc, str, i) => {
		return acc + str + (values[i] ?? '');
	}, '');
}

/**
 * Helper to create cache keys for queries
 */
export function createCacheKey(operation: string, variables?: Record<string, unknown>): string {
	const varsString = variables ? JSON.stringify(variables) : '';
	return `gql:${operation}:${varsString}`;
}

/**
 * Fragment builder for reusable GraphQL fragments
 * 
 * @example
 * ```ts
 * const userFragment = buildFragment`
 *   fragment UserFields on User {
 *     id
 *     name
 *     email
 *   }
 * `;
 * 
 * const query = buildQuery`
 *   ${userFragment}
 *   query GetUsers {
 *     users {
 *       ...UserFields
 *     }
 *   }
 * `;
 * ```
 */
export function buildFragment(
	strings: TemplateStringsArray,
	...values: unknown[]
): string {
	return strings.reduce((acc, str, i) => {
		return acc + str + (values[i] ?? '');
	}, '');
}
