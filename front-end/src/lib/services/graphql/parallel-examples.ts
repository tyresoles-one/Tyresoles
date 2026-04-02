/**
 * Examples of running multiple GraphQL queries in parallel
 * 
 * The GraphQL service fully supports parallel query execution.
 * Each query runs independently and can execute concurrently.
 */

import { graphqlQuery, buildQuery } from './index';

// ============================================================================
// Example 1: Basic Parallel Queries
// ============================================================================

export async function exampleBasicParallel() {
	const usersQuery = buildQuery`
		query GetUsers {
			users {
				id
				name
			}
		}
	`;

	const postsQuery = buildQuery`
		query GetPosts {
			posts {
				id
				title
			}
		}
	`;

	// Both queries execute in parallel
	const [usersResult, postsResult] = await Promise.all([
		graphqlQuery(usersQuery, { cacheKey: 'users' }),
		graphqlQuery(postsQuery, { cacheKey: 'posts' })
	]);

	if (usersResult.success && postsResult.success) {
		console.log('Users:', usersResult.data);
		console.log('Posts:', postsResult.data);
	}
}

// ============================================================================
// Example 2: Parallel Queries with Different Variables
// ============================================================================

export async function exampleParallelWithVariables() {
	const query = buildQuery`
		query GetUser($id: ID!) {
			user(id: $id) {
				id
				name
				email
			}
		}
	`;

	// These run in parallel because they have different variables
	const [user1, user2, user3] = await Promise.all([
		graphqlQuery(query, { variables: { id: '1' }, cacheKey: 'user-1' }),
		graphqlQuery(query, { variables: { id: '2' }, cacheKey: 'user-2' }),
		graphqlQuery(query, { variables: { id: '3' }, cacheKey: 'user-3' })
	]);

	return { user1, user2, user3 };
}

// ============================================================================
// Example 3: Parallel Queries with Error Handling
// ============================================================================

export async function exampleParallelWithErrorHandling() {
	const queries = [
		graphqlQuery(buildQuery`query GetUsers { users { id } }`, { cacheKey: 'users' }),
		graphqlQuery(buildQuery`query GetPosts { posts { id } }`, { cacheKey: 'posts' }),
		graphqlQuery(buildQuery`query GetComments { comments { id } }`, { cacheKey: 'comments' })
	];

	// Use Promise.allSettled to handle partial failures
	const results = await Promise.allSettled(queries);

	results.forEach((result, index) => {
		if (result.status === 'fulfilled') {
			if (result.value.success) {
				console.log(`Query ${index} succeeded:`, result.value.data);
			} else {
				console.error(`Query ${index} failed:`, result.value.error);
			}
		} else {
			console.error(`Query ${index} rejected:`, result.reason);
		}
	});
}

// ============================================================================
// Example 4: Sequential vs Parallel Performance
// ============================================================================

export async function exampleSequentialVsParallel() {
	const query = buildQuery`
		query GetData($id: ID!) {
			data(id: $id) {
				id
				value
			}
		}
	`;

	// Sequential execution (slower)
	console.time('Sequential');
	const sequential1 = await graphqlQuery(query, { variables: { id: '1' } });
	const sequential2 = await graphqlQuery(query, { variables: { id: '2' } });
	const sequential3 = await graphqlQuery(query, { variables: { id: '3' } });
	console.timeEnd('Sequential'); // ~300ms if each takes 100ms

	// Parallel execution (faster)
	console.time('Parallel');
	const [parallel1, parallel2, parallel3] = await Promise.all([
		graphqlQuery(query, { variables: { id: '1' } }),
		graphqlQuery(query, { variables: { id: '2' } }),
		graphqlQuery(query, { variables: { id: '3' } })
	]);
	console.timeEnd('Parallel'); // ~100ms (all run concurrently)
}

// ============================================================================
// Example 5: Request Deduplication (Same Query + Same Variables)
// ============================================================================

export async function exampleDeduplication() {
	const query = buildQuery`
		query GetUsers {
			users {
				id
				name
			}
		}
	`;

	// These three calls will result in ONLY ONE network request
	// because they have the same query and variables
	const [result1, result2, result3] = await Promise.all([
		graphqlQuery(query, { cacheKey: 'users' }),
		graphqlQuery(query, { cacheKey: 'users' }),
		graphqlQuery(query, { cacheKey: 'users' })
	]);

	// All three will receive the same data from the single request
	console.log('All results are identical:', 
		result1.data === result2.data && result2.data === result3.data
	);
}

// ============================================================================
// Example 6: Mixed Parallel and Sequential
// ============================================================================

export async function exampleMixedExecution() {
	// First, run some queries in parallel
	const [users, posts] = await Promise.all([
		graphqlQuery(buildQuery`query GetUsers { users { id } }`),
		graphqlQuery(buildQuery`query GetPosts { posts { id } }`)
	]);

	// Then use the results to run dependent queries
	if (users.success && posts.success) {
		const userId = users.data?.users[0]?.id;
		const postId = posts.data?.posts[0]?.id;

		// These can also run in parallel
		const [userDetails, postDetails] = await Promise.all([
			graphqlQuery(buildQuery`query GetUser($id: ID!) { user(id: $id) { name } }`, {
				variables: { id: userId }
			}),
			graphqlQuery(buildQuery`query GetPost($id: ID!) { post(id: $id) { title } }`, {
				variables: { id: postId }
			})
		]);

		return { userDetails, postDetails };
	}
}

// ============================================================================
// Example 7: Using with Svelte Hooks (Parallel Queries)
// ============================================================================

/**
 * In a Svelte component, you can use multiple hooks in parallel:
 * 
 * ```svelte
 * <script>
 *   import { useGraphQLQuery, buildQuery } from '$lib/services/graphql';
 *   
 *   const usersQuery = buildQuery`query GetUsers { users { id name } }`;
 *   const postsQuery = buildQuery`query GetPosts { posts { id title } }`;
 *   
 *   // Both queries execute in parallel automatically
 *   const users = useGraphQLQuery(usersQuery, { cacheKey: 'users' });
 *   const posts = useGraphQLQuery(postsQuery, { cacheKey: 'posts' });
 * </script>
 * 
 * {#if users.loading || posts.loading}
 *   <p>Loading...</p>
 * {:else}
 *   <div>
 *     <h2>Users</h2>
 *     {#each users.data?.users || [] as user}
 *       <p>{user.name}</p>
 *     {/each}
 *     
 *     <h2>Posts</h2>
 *     {#each posts.data?.posts || [] as post}
 *       <p>{post.title}</p>
 *     {/each}
 *   </div>
 * {/if}
 * ```
 */

// ============================================================================
// Example 8: Batch Multiple Queries
// ============================================================================

export async function exampleBatchQueries() {
	const queries = [
		{ query: buildQuery`query GetUsers { users { id } }`, key: 'users' },
		{ query: buildQuery`query GetPosts { posts { id } }`, key: 'posts' },
		{ query: buildQuery`query GetComments { comments { id } }`, key: 'comments' },
		{ query: buildQuery`query GetTags { tags { id } }`, key: 'tags' }
	];

	// Execute all queries in parallel
	const results = await Promise.all(
		queries.map(({ query, key }) =>
			graphqlQuery(query, { cacheKey: key })
		)
	);

	// Process results
	const data = results.reduce((acc, result, index) => {
		if (result.success && result.data) {
			acc[queries[index].key] = result.data;
		}
		return acc;
	}, {} as Record<string, unknown>);

	return data;
}
