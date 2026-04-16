import { secureFetch } from "$lib/services/api";
import { graphqlQuery } from "$lib/services/graphql";
import { GetMyDocumentsQuery } from "$lib/services/graphql/operations/sales/queries";

export interface Document {
    no: string;
    date: string;
    customerNo: string;
    name: string;
    amount: number;
}

export async function fetchDocuments(view: string, respCenters: string[], search?: string, skip: number = 0, take: number = 50): Promise<Document[]> {
    const res = await graphqlQuery<any>(GetMyDocumentsQuery, {
        variables: {
            input: {
                view,
                respCenters,
                type: "GST",
                search,
                skip,
                take,
                from: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(), // Last 30 days
                to: new Date().toISOString()
            }
        }
    });
    const rows = res.data?.getMyDocuments || [];
    return rows;
}

export async function verifyEInvoice(type: string, no: string): Promise<{ pdf: string }> {
    const response = await secureFetch(`/api/protean/verify-einv?type=${type}&no=${no}`);
    if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || "Failed to verify E-Invoice");
    }
    return response.json();
}
