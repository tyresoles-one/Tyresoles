export interface GSTINResponse {
    addrBnm: string;
    addrBno: string;
    addrFlno: string;
    addrLoc: string;
    addrPncd: string;
    addrSt: string;
    blkStatus: string;
    dtDReg: string;
    dtReg: string;
    gstin: string;
    legalName: string;
    stateCode: string;
    status: string;
    tradeName: string;
    txpType: string;
}

export interface PartyGstin {
    type: string;
    code: string;
    name: string;
    gstin: string;
    status: string;
    blockStatus: string;
    tradeName: string;
    legalName: string;
    respCenter: string;
}
