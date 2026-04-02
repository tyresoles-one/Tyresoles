import { secureFetch } from "$lib/services/api";
import { graphqlQuery, buildQuery } from "$lib/services/graphql";
import { GetMyCustomersDocument, GetMyVendorsDocument } from "$lib/services/graphql/generated/types";
import type { PartyGstin, GSTINResponse } from "./types";

const GET_MY_VEHICLES_SEARCH = buildQuery`
    query GetMyVehiclesSearch($where: VehiclesFilterInput, $first: Int) {
        myVehicles(where: $where, first: $first) {
            nodes { no name status }
        }
    }
`;

export async function fetchPartyName(type: string, code: string): Promise<{ name: string }> {
    // For Vehicles/Transporter, look up name via GraphQL instead of REST
    if (type === 'Transporter') {
        const res = await graphqlQuery<any>(GET_MY_VEHICLES_SEARCH, {
            variables: { where: { no: { eq: code } }, first: 1 },
            skipLoading: true, skipCache: true
        });
        const node = res.data?.myVehicles?.nodes?.[0];
        if (node) return { name: node.name ?? '' };
        return { name: '' };
    }
    const url = `/api/protean/party/${type}/${code}`;
    const res = await secureFetch(url);
    if (!res.ok) throw new Error("Party not found");
    return await res.json();
}

export async function searchParties(type: string, search: string): Promise<{ label: string, value: string }[]> {
    if (type === 'Customer') {
        const res = await graphqlQuery<any>(GetMyCustomersDocument, {
            variables: {
                where: {
                    or: [
                        { no: { contains: search } },
                        { name: { contains: search } }
                    ]
                },
                first: 20
            }
        });
        if (res.success && res.data?.myCustomers?.nodes) {
            return res.data.myCustomers.nodes.map((n: any) => ({
                label: `${n.no} - ${n.name}`,
                value: n.no,
                name: n.name
            }));
        }
    } else if (type === 'Vendor') {
        const res = await graphqlQuery<any>(GetMyVendorsDocument, {
            variables: {
                where: {
                    or: [
                        { no: { contains: search } },
                        { name: { contains: search } }
                    ]
                },
                first: 20
            }
        });
        if (res.success && res.data?.myVendors?.nodes) {
            return res.data.myVendors.nodes.map((n: any) => ({
                label: `${n.no} - ${n.name}`,
                value: n.no,
                name: n.name
            }));
        }
    } else if (type === 'Transporter') {
        const res = await graphqlQuery<any>(GET_MY_VEHICLES_SEARCH, {
            variables: {
                where: {
                    or: [
                        { no: { contains: search } },
                        { name: { contains: search } }
                    ]
                },
                first: 20
            },
            skipLoading: true, skipCache: true
        });
        if (res.success && res.data?.myVehicles?.nodes) {
            return res.data.myVehicles.nodes.map((n: any) => ({
                label: `${n.no} - ${n.name}${n.status === 1 ? ' (Inactive)' : ''}`,
                value: n.no,
                name: n.name
            }));
        }
    }
    return [];
}


export async function verifyGstin(gstin: string): Promise<GSTINResponse> {
    const url = `/api/protean/gstin/${gstin}`;
    const res = await secureFetch(url);
    if (!res.ok) throw new Error("GSTIN verification failed");
    return await res.json();
}

export async function syncGstin(gstin: string): Promise<GSTINResponse> {
    const url = `/api/protean/gstin/${gstin}/sync`;
    const res = await secureFetch(url);
    if (!res.ok) throw new Error("GSTIN sync failed");
    return await res.json();
}

export async function saveGstin(data: PartyGstin): Promise<{ success: boolean }> {
    const url = `/api/protean/gstin/save`;
    const res = await secureFetch(url, {
        method: "POST",
        body: JSON.stringify(data)
    });
    if (!res.ok) throw new Error("Failed to save GSTIN");
    return await res.json();
}
