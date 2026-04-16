import type { FetchParams } from "./models";
import type { FetchParamsInput } from "$lib/services/graphql/generated/graphql";

/** Maps session `FetchParams` to GraphQL `FetchParamsInput` (all scalar/list fields required by schema). */
export function toFetchParamsInput(p: FetchParams): FetchParamsInput {
  return {
    areas: (p.areas as string[]) ?? [],
    from: (p.from as string) ?? "",
    nos: (p.nos as string[]) ?? [],
    regions: p.regions ?? [],
    reportName: (p.reportName as string) ?? "",
    respCenters: p.respCenters ?? [],
    to: (p.to as string) ?? "",
    type: p.type ?? "",
    userCode: p.userCode ?? "",
    userDepartment: p.userDepartment ?? "",
    userSpecialToken: (p.userSpecialToken as string) ?? "",
    userType: (p.userType as string) ?? "",
    view: p.view ?? "",
  };
}
