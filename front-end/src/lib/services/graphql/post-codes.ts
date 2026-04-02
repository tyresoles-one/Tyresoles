import gql from "graphql-tag";
import type { InputMaybe } from "./generated/types.js";
import type { StringOperationFilterInput } from "./generated/types.js";

/** Hot Chocolate filter input for NAV Post Code (mirrors generated schema after `GetPostCodes` is deployed). */
export type PostCodeFilterInput = {
	and?: InputMaybe<Array<PostCodeFilterInput>>;
	or?: InputMaybe<Array<PostCodeFilterInput>>;
	code?: InputMaybe<StringOperationFilterInput>;
	city?: InputMaybe<StringOperationFilterInput>;
	searchCity?: InputMaybe<StringOperationFilterInput>;
	stateCode?: InputMaybe<StringOperationFilterInput>;
	county?: InputMaybe<StringOperationFilterInput>;
	countryRegionCode?: InputMaybe<StringOperationFilterInput>;
};

export type GetPostCodesQueryVariables = {
	first?: number | null;
	after?: string | null;
	where?: PostCodeFilterInput | null;
};

export type PostCodeNode = {
	code: string | null;
	city: string | null;
	searchCity: string | null;
	stateCode: string | null;
	county: string | null;
	countryRegionCode: string | null;
};

export type GetPostCodesQuery = {
	postCodes?: {
		totalCount: number;
		items: PostCodeNode[] | null;
		pageInfo: {
			hasNextPage: boolean;
			hasPreviousPage: boolean;
			startCursor: string | null;
			endCursor: string | null;
		};
	} | null;
};

export const GetPostCodesDocument = gql`
	query GetPostCodes(
		$first: Int
		$after: String
		$where: PostCodeFilterInput
	) {
		postCodes(first: $first, after: $after, where: $where) {
			items: nodes {
				code
				city
				searchCity
				stateCode
				county
				countryRegionCode
			}
			pageInfo {
				hasNextPage
				hasPreviousPage
				startCursor
				endCursor
			}
			totalCount
		}
	}
`;
