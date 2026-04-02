/**
 * Sales Report GraphQL Queries
 */

export const GetMyDocumentsQuery = `
  query GetMyDocuments($input: SalesReportParamsInput!) {
    getMyDocuments(parameters: $input) {
      no
      date
      customerNo
      name
      amount
    }
  }
`;
