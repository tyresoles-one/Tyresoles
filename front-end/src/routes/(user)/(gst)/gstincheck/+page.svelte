<script lang="ts">
    import { z } from "zod";
    import { CreateForm, FormGenerator, type FormSchema } from "$lib/components/venUI/form";
    import { toast } from "$lib/components/venUI/toast";
    import { Button } from "$lib/components/ui/button";
    import { Icon } from "$lib/components/venUI/icon";
    import { PageHeading } from "$lib/components/venUI/page-heading";
    import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
    import { authStore } from "$lib/stores/auth";
    import * as api from "./api";
    import type { GSTINResponse, PartyGstin } from "./types";
    import Card from "./card.svelte";

    const user = $derived(authStore.get().user);
    const locations = $derived(authStore.get().locations ?? []);
    const showCenterSelect = $derived(locations.length > 1);

    let cardData = $state<GSTINResponse | null>(null);
    let loading = $state(false);

    const schema_z = z.object({
        type: z.string().min(1, "Please select party type"),
        code: z.string().min(3, "Please enter at least 3 characters"),
        name: z.string().default(''),
        gstin: z.string().length(15, "GSTIN must be 15 characters"),
        status: z.string().default(''),
        blockStatus: z.string().default(''),
        tradeName: z.string().default(''),
        legalName: z.string().default(''),
        respCenter: z.string().min(1, "Please select responsibility center")
    });

    const form = CreateForm<PartyGstin>({
        schema: schema_z,
        initialValues: {
            type: '',
            code: '',
            name: '',
            gstin: '',
            status: '',
            blockStatus: '',
            tradeName: '',
            legalName: '',
            respCenter: authStore.get().user?.respCenter ?? ''
        },
        onSubmit: async (values) => {
            if (!values.type || !values.code) {
                toast.error("Please select party type and code");
                return;
            }
            if (!values.gstin || values.gstin.length < 15) {
                toast.error("Please enter valid GSTIN");
                return;
            }
            if (!cardData) {
                toast.error("Please verify GSTIN");
                return;
            }

            loading = true;
            try {
                const res = await api.saveGstin({
                    ...values,
                    status: cardData.status,
                    blockStatus: cardData.blkStatus ?? 'U',
                    tradeName: cardData.tradeName,
                    legalName: cardData.legalName
                });
                if (res.success) {
                    toast.success("GSTIN saved successfully in ERP");
                    form.reset();
                    cardData = null;
                }
            } catch (err: any) {
                toast.error(err.message || "Failed to save GSTIN");
            } finally {
                loading = false;
            }
        }
    });

    $effect(() => {
        const code = form.values.code;
        const type = form.values.type;
        if (code && type && code.length >= 3) {
            api.fetchPartyName(type, code).then(res => {
                form.setValue('name', res.name);
            }).catch(() => {
                form.setValue('name', '');
            });
        }
    });

    async function handleVerify() {
        const gstin = form.values.gstin;
        if (!gstin || gstin.length < 15) {
            toast.error("Please enter a 15-digit GSTIN first");
            return;
        }

        loading = true;
        try {
            const res = await api.verifyGstin(gstin);
            cardData = res;
            toast.success("GSTIN details verified!");
        } catch (err: any) {
            toast.error(err.message || "GSTIN verification failed");
            cardData = null;
        } finally {
            loading = false;
        }
    }

    async function handleSync() {
        const gstin = form.values.gstin;
        if (!gstin || gstin.length < 15) {
            toast.error("Please enter a valid GSTIN");
            return;
        }

        loading = true;
        try {
            const res = await api.syncGstin(gstin);
            cardData = res;
            toast.success("GSTIN details synchronized with Portal");
        } catch (err: any) {
            toast.error(err.message || "GSTIN sync failed");
        } finally {
            loading = false;
        }
    }

    const schema = $derived.by<FormSchema>(() => {
        const type = form.values.type;
        const currentCenter = (form.values.respCenter as string) || user?.respCenter || '';
        
        return [
            {
                type: 'grid' as const,
                cols: 2,
                gap: 6,
                children: [
                    ...(showCenterSelect ? [{
                        type: 'custom' as const,
                        component: MasterSelect,
                        props: {
                            fieldName: 'respCenter',
                            masterType: 'respCenters',
                            label: 'Resp. Center',
                            respCenterType: 'Sale',
                            singleSelect: true
                        },
                        required: true,
                        colSpan: 1
                    }] : []),
                    {
                        type: 'field' as const,
                        name: 'type',
                        label: 'Party Type',
                        inputType: 'select',
                        options: [
                            { label: 'Customer', value: 'Customer' },
                            { label: 'Vendor', value: 'Vendor' },
                            { label: 'Transporter', value: 'Transporter' }
                        ],
                        required: true
                    },
                    (type === 'Transporter' ? {
                        type: 'custom' as const,
                        component: MasterSelect,
                        props: {
                            fieldName: 'code',
                            masterType: 'vehicles',
                            label: 'Transporter (Vehicle)',
                            placeholder: 'Search vehicle No / Name...',
                            disabled: false,
                            singleSelect: true,
                            respCenterOverride: [currentCenter]
                        },
                        required: true,
                        colSpan: 1
                    } : {
                        type: 'custom' as const,
                        component: MasterSelect,
                        props: {
                            fieldName: 'code',
                            masterType: type === 'Customer' ? 'customers' : 'vendors',
                            label: 'Party Code',
                            disabled: !type,
                            singleSelect: true,
                            respCenterOverride: currentCenter
                        },
                        required: true,
                        colSpan: 1
                    }),                    
                    {
                        type: 'field' as const,
                        name: 'name',
                        label: 'Party Name',
                        disabled: true,
                        class: 'bg-muted/50 font-semibold'
                    },
                    {
                        type: 'field' as const,
                        name: 'gstin',
                        label: 'GSTIN',
                        placeholder: '15-digit GSTIN',
                        onBlur: handleVerify,
                        class: 'font-mono'
                    }
                ]
            }
        ];
    });
</script>

<svelte:head>
    <title>GSTIN Check | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background pb-20">
    <PageHeading backHref="/" icon="clipboard-check">
        {#snippet title()}
            <span class="truncate">GSTIN Master Check</span>
        {/snippet}

        {#snippet description()}
            <div class="flex items-center gap-1.5 text-primary">
                <Icon name="shield-check" class="size-3" />
                <span class="font-bold">Direct IRP Portal Link</span>
            </div>
        {/snippet}

        {#snippet actions()}
            <div class="flex items-center gap-2">
                <Button variant="outline" size="sm" onclick={handleSync} disabled={loading || form.isSubmitting} class="h-9 px-4 rounded-xl font-bold border-primary/20 hover:bg-primary/5">
                    <Icon name="refresh-cw" class="mr-2 h-4 w-4 {loading ? 'animate-spin' : ''}" />
                    <span class="hidden sm:inline">Sync Portal</span>
                    <span class="sm:hidden">Sync</span>
                </Button>
                <Button size="sm" onclick={() => form.submit()} disabled={loading || form.isSubmitting} class="h-9 px-4 rounded-xl font-bold shadow-lg shadow-primary/20">
                    <Icon name="save" class="mr-2 h-4 w-4" />
                    Update Master
                </Button>
            </div>
        {/snippet}
    </PageHeading>

    <div class="container mx-auto p-4 sm:p-6 lg:p-8 space-y-8">
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            <div class="lg:col-span-7 bg-card rounded-3xl border border-border/60 p-6 sm:p-10 shadow-2xl shadow-primary/5 relative overflow-hidden group">
                <!-- Subtle decorative background element -->
                <div class="absolute -top-24 -right-24 size-48 bg-primary/5 rounded-full blur-3xl group-hover:bg-primary/10 transition-colors duration-700"></div>
                
                <div class="relative">
                    <h3 class="text-sm font-bold uppercase tracking-widest text-muted-foreground/80 mb-8 flex items-center gap-3">
                        <div class="size-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center">
                            <Icon name="user" class="h-4 w-4" />
                        </div>
                        Party Authentication
                    </h3>
                    <FormGenerator {form} {schema} root={true} autoFocus={true} />

                    <div class="mt-8 pt-6 border-t border-border/40 flex justify-end">
                        <Button 
                            variant="secondary" 
                            onclick={handleVerify} 
                            disabled={loading || form.values.gstin.length < 15}
                            class="rounded-xl font-bold px-8 h-12 shadow-sm border border-border/50 hover:bg-primary/5 hover:text-primary transition-all duration-300"
                        >
                            {#if loading}
                                <Icon name="loader-circle" class="mr-2 h-4 w-4 animate-spin" />
                                VERIFYING...
                            {:else}
                                <Icon name="shield-check" class="mr-2 h-4 w-4" />
                                VERIFY GSTIN
                            {/if}
                        </Button>
                    </div>
                </div>
            </div>

            <div class="lg:col-span-5">
                {#if cardData}
                    <div class="animate-in fade-in slide-in-from-right-4 duration-500">
                        <h3 class="text-sm font-bold uppercase tracking-widest text-primary mb-5 flex items-center gap-3 px-1">
                            <div class="size-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center">
                                <Icon name="circle-check" class="h-4 w-4" />
                            </div>
                            Portal Verification Results
                        </h3>
                        <div class="relative">
                            <Card details={cardData} />
                            <!-- Success indicator -->
                            <div class="absolute -top-2 -right-2 size-6 bg-green-500 text-white rounded-full flex items-center justify-center shadow-lg border-2 border-background animate-bounce">
                                <Icon name="check" class="size-3" />
                            </div>
                        </div>
                    </div>
                {:else}
                    <div class="h-full min-h-[420px] flex flex-col items-center justify-center text-center p-12 border-2 border-dashed border-border/30 rounded-3xl bg-muted/10 transition-all duration-500 hover:border-primary/30 hover:bg-primary/5 group">
                        <div class="bg-background rounded-2xl p-6 mb-6 shadow-xl border border-border/50 group-hover:scale-110 transition-transform duration-500">
                            <Icon name="search" class="h-10 w-10 text-muted-foreground/30 group-hover:text-primary/40" />
                        </div>
                        <h4 class="font-bold text-xl text-foreground/70">Ready to Verify</h4>
                        <p class="text-sm text-muted-foreground mt-3 max-w-[240px] leading-relaxed">Enter valid party details and click verify to fetch live records from the GST Portal.</p>
                        
                        <div class="mt-8 flex gap-2">
                            <div class="size-2 rounded-full bg-primary/20 animate-pulse"></div>
                            <div class="size-2 rounded-full bg-primary/20 animate-pulse delay-75"></div>
                            <div class="size-2 rounded-full bg-primary/20 animate-pulse delay-150"></div>
                        </div>
                    </div>
                {/if}
            </div>
        </div>
    </div>
</div>

<style>
    :global(.bg-muted\/50 input) {
        cursor: not-allowed;
    }
</style>
