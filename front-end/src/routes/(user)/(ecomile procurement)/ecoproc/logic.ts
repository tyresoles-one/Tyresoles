import type { FetchParams, Vendor, CodeName } from '$lib/business/models';
import { fetchParamsStore, vendorStore, itemsStore, makesStore, 
    inspectorsStore, procInspectionsStore, statesStore, marketsStore } from '$lib/managers/stores';
import { getStore } from '$lib';
import { toast } from '$lib/components';
import { graphqlQuery } from '$lib/services/graphql';
import { 
    GetProductionVendorsDocument,
    GetProductionItemNosDocument,
    GetProductionMakesDocument,
    GetProductionInspectorCodeNamesDocument,
    GetProductionProcurementInspectionDocument,
    GetProductionProcMarketsDocument,
    GetGroupDetailsDocument,
    InsertProductionCasingItemsDocument,
} from '$lib/services/graphql/generated/graphql';
import type {
    GetProductionVendorsQuery, GetProductionVendorsQueryVariables,
    GetProductionItemNosQuery, GetProductionItemNosQueryVariables,
    GetProductionMakesQuery, GetProductionMakesQueryVariables,
    GetProductionInspectorCodeNamesQuery, GetProductionInspectorCodeNamesQueryVariables,
    GetProductionProcurementInspectionQuery, GetProductionProcurementInspectionQueryVariables,
    GetProductionProcMarketsQuery, GetProductionProcMarketsQueryVariables,
    GetGroupDetailsQuery, GetGroupDetailsQueryVariables,
    InsertProductionCasingItemsMutation, InsertProductionCasingItemsMutationVariables,
    CasingItemInput,
} from '$lib/services/graphql/generated/graphql';
import { toFetchParamsInput } from '$lib/business/fetch-params';

export { toFetchParamsInput };

export const fetchMasters = () => {
    Promise.all([
        fetchVendors(),
        fetchItems(),
        fetchMakes(),
        fetchInspectors(),
        fetchProcInspections()])    
        .then(([resVender, resItems, resMakes, 
            resInspectors, resProcInspections]) => {
                if(resVender?.success) vendorStore.set(resVender.data ?? null);
                if(resItems?.success) itemsStore.set(resItems.data ?? null);
                if(resMakes?.success) makesStore.set(resMakes.data ?? null);
                if(resInspectors?.success) inspectorsStore.set(resInspectors.data ?? null);
                if(resProcInspections?.success) procInspectionsStore.set(resProcInspections.data ?? null);
            });
}

export const fetchStatesAndMarkets = () => {
    return Promise.all([fetchStates(), fetchMarkets()]).then(
        ([resStates, resMarkets]) => {
            if (resStates?.success) statesStore.set(resStates.data ?? null);
            if (resMarkets?.success) marketsStore.set(resMarkets.data ?? null);
        }
    );
};

export const fetchVendors = async (view: string = '') => {
    let fetchParams: FetchParams|null  = getStore(fetchParamsStore);
    if (!fetchParams){
        toast.error('Please do relogin.! [Empty Fetch Params]');
        return { success: false, message: 'Empty Fetch Params' };
    }
    const param = {...fetchParams, regions: ['CASING PROCUREMENT'], view};
    const result = await graphqlQuery<GetProductionVendorsQuery, GetProductionVendorsQueryVariables>(GetProductionVendorsDocument, {
        variables: { param: toFetchParamsInput(param) }
    });
    
    if (result.success && result.data) {
        return { success: true, data: (result.data.productionVendors as Vendor[]) ?? [] };
    }
    return { success: false, message: result.error };
};

export const fetchItems = async (itemCategory: string | Array<string> = 'CASING', type: string = 'FromGroupDetail', respCenter: string = '') => {
    let fetchParams: FetchParams|null  = getStore(fetchParamsStore);
    if (!fetchParams){
        toast.error('Please do relogin.! [Empty Fetch Params]');
        return { success: false, message: 'Empty Fetch Params' };
    }
    const param = { ...fetchParams, regions: typeof itemCategory === 'string' ? [itemCategory] : itemCategory, type }; 
    if(respCenter !== '')
        param.respCenters = [respCenter];
        
    const result = await graphqlQuery<GetProductionItemNosQuery, GetProductionItemNosQueryVariables>(GetProductionItemNosDocument, {
        variables: { param: toFetchParamsInput(param) }
    });

    if (result.success && result.data) {
        return { success: true, data: (result.data.productionItemNos as any[]) ?? [] };
    }
    return { success: false, message: result.error };
}

export const fetchMakes = async (itemCategory: string | Array<string> = 'TYREMAKE', itemGroup: string = 'casing') => {
    let fetchParams: FetchParams|null  = getStore(fetchParamsStore);
    if (!fetchParams){
        toast.error('Please do relogin.! [Empty Fetch Params]');
        return { success: false, message: 'Empty Fetch Params' };
    }
    const param = { ...fetchParams, regions: typeof itemCategory === 'string' ? [itemCategory] : itemCategory, type: itemGroup };     
    const result = await graphqlQuery<GetProductionMakesQuery, GetProductionMakesQueryVariables>(GetProductionMakesDocument, {
        variables: { param: toFetchParamsInput(param) }
    });

    if (result.success && result.data) {
        return { success: true, data: (result.data.productionMakes as CodeName[]) ?? [] };
    }
    return { success: false, message: result.error };
}

export const fetchInspectors = async () => {
    let fetchParams: FetchParams|null  = getStore(fetchParamsStore);
    if (!fetchParams){
        toast.error('Please do relogin.! [Empty Fetch Params]');
        return { success: false, message: 'Empty Fetch Params' };
    }
    const result = await graphqlQuery<GetProductionInspectorCodeNamesQuery, GetProductionInspectorCodeNamesQueryVariables>(GetProductionInspectorCodeNamesDocument, {
        variables: { param: toFetchParamsInput(fetchParams) }
    });

    if (result.success && result.data) {
        return { success: true, data: (result.data.productionInspectorCodeNames as CodeName[]) ?? [] };
    }
    return { success: false, message: result.error };
}

export const fetchProcInspections = async () => {
    let fetchParams: FetchParams|null  = getStore(fetchParamsStore);
    if (!fetchParams){
        toast.error('Please do relogin.! [Empty Fetch Params]');
        return { success: false, message: 'Empty Fetch Params' };
    }
    const result = await graphqlQuery<GetProductionProcurementInspectionQuery, GetProductionProcurementInspectionQueryVariables>(GetProductionProcurementInspectionDocument, {
        variables: { param: toFetchParamsInput(fetchParams) }
    });

    if (result.success && result.data) {
        return { success: true, data: (result.data.productionProcurementInspection as CodeName[]) ?? [] };
    }
    return { success: false, message: result.error };
}

export const fetchStates = async () => {
    const result = await graphqlQuery<GetGroupDetailsQuery, GetGroupDetailsQueryVariables>(GetGroupDetailsDocument, {
        variables: { category: 'STATE' }
    });

    if (result.success && result.data) {
        const states = (result.data.groupDetails ?? []).map(s => ({ code: s.code, name: s.name }));
        return { success: true, data: states as CodeName[] };
    }
    return { success: false, message: result.error };
};

export const fetchMarkets = async () => {
    let fetchParams: FetchParams|null  = getStore(fetchParamsStore);
    if (!fetchParams){
        toast.error('Please do relogin.! [Empty Fetch Params]');
        return { success: false, message: 'Empty Fetch Params' };
    }
    const result = await graphqlQuery<GetProductionProcMarketsQuery, GetProductionProcMarketsQueryVariables>(GetProductionProcMarketsDocument, {
        variables: { param: toFetchParamsInput(fetchParams) }
    });

    if (result.success && result.data) {
        // This query returns string[] in the schema but some might return objects.
        // Based on Step 734 'GetProductionProcMarkets' has NO nested selection set, returning string[]
        const data = (result.data.productionProcMarkets ?? []).map((m: any) => ({ code: m, name: m }));
        return { success: true, data: data as CodeName[] };
    }
    return { success: false, message: result.error };
};

export const insertCasingItems = async (items: CasingItemInput[]) => {
    const result = await graphqlQuery<InsertProductionCasingItemsMutation, InsertProductionCasingItemsMutationVariables>(InsertProductionCasingItemsDocument, {
        variables: { casingItems: items }
    });
    if (result.success && result.data?.insertProductionCasingItems.success) {
        toast.success(result.data.insertProductionCasingItems.message || 'Items saved successfully');
        return { success: true };
    }
    toast.error(result.error || result.data?.insertProductionCasingItems.message || 'Failed to save items');
    return { success: false };
};