/**
 * GraphQL Error type
 */
export interface GraphQLError {
	message: string;
	locations?: Array<{ line: number; column: number }>;
	path?: Array<string | number>;
	extensions?: Record<string, unknown>;
}

/**
 * GraphQL Response type
 */
export interface GraphQLResponse<TData = unknown> {
	data?: TData;
	errors?: GraphQLError[];
	extensions?: Record<string, unknown>;
}

/**
 * GraphQL Request Options
 */
export interface GraphQLRequestOptions {
	operationName?: string;
	variables?: Record<string, unknown>;
}
