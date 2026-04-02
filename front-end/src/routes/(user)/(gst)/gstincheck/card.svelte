<script lang="ts">
    import type { GSTINResponse } from "./types";
    
    let { details } = $props<{ details: GSTINResponse }>();

    const gstStatus = (status: string): string => {
        switch (status) {
            case 'ACT': return 'Active';
            case 'CNL': return 'Cancelled';
            case 'INA': return 'Inactive';
            case 'PRO': return 'Provision';
            default: return status;
        }
    };

    const sanitizeData = (input: unknown): string => {
        return typeof input === 'string' ? input : '';
    };
</script>

{#if details}
    <div class="rounded-lg bg-muted/30 p-4 border border-border/50 space-y-2">
        {@render lableValue('Trade Name', details.tradeName)}
        {@render lableValue('Legal Name', details.legalName)}
        {@render lableValue(
            'Address',
            `${sanitizeData(details.addrBno)} ${sanitizeData(details.addrBnm)} ${sanitizeData(details.addrSt)} ${sanitizeData(
                details.addrLoc
            )} ${sanitizeData(details.addrPncd)}`.trim()
        )}
        {@render lableValue('Date of Reg', details.dtReg)}
        {@render lableValue('Status', gstStatus(details.status))}
        {@render lableValue('BlockStatus', details.blkStatus === 'B' ? 'Blocked' : 'Unblocked')}
        {#if details.dtDReg}
            {@render lableValue('Date of De-Reg', details.dtDReg)}
        {/if}
    </div>
{/if}

{#snippet lableValue(label: string, value: string)}
    {#if value}
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-1 p-2 rounded-md bg-card/50 border border-border/20">
            <span class="text-sm font-medium text-muted-foreground">{label}</span>
            <span class="text-sm font-semibold text-foreground bg-background/50 px-3 py-1 rounded-sm border border-border/10 flex-1 sm:max-w-[70%] text-right">{value}</span>
        </div>
    {/if}
{/snippet}
