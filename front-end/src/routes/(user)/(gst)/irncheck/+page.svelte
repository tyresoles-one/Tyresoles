<script lang="ts">
    import { z } from "zod";
    import { CreateForm, FormGenerator, type FormSchema } from "$lib/components/venUI/form";
    import { toast } from "$lib/components/venUI/toast";
    import { Button } from "$lib/components/ui/button";
    import { Icon } from "$lib/components/venUI/icon";
    import { PageHeading } from "$lib/components/venUI/page-heading";
    import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
    import DocumentSelect from "./DocumentSelect.svelte";
    import { authStore } from "$lib/stores/auth";
    import * as api from "./api";
    import type { Document } from "./api";
    import PdfViewer from "$lib/components/venUI/pdf-viewer/PdfViewer.svelte";

    const user = $derived(authStore.get().user);
    const locations = $derived(authStore.get().locations ?? []);
    const showCenterSelect = $derived(locations.length > 1);

    let verifying = $state(false);
    let pdfData = $state<Uint8Array | null>(null);

    const schema_z = z.object({
        respCenter: z.string().min(1, "Please select responsibility center"),
        docType: z.string().min(1, "Please select document type"),
        docNo: z.string().min(1, "Please select document number")
    });

    const form = CreateForm({
        schema: schema_z,
        initialValues: {
            respCenter: authStore.get().user?.respCenter ?? '',
            docType: 'Invoice',
            docNo: ''
        },
        onSubmit: async (values) => {
            handleVerify();
        }
    });

    // Reset docNo if docType or respCenter changes
    $effect(() => {
        form.values.docType;
        form.values.respCenter;
        form.setValue('docNo', '');
    });

    async function handleVerify() {
        if (!form.values.docType || !form.values.docNo) {
            toast.error("Please select both document type and number");
            return;
        }

        verifying = true;
        pdfData = null;
        try {
            const res = await api.verifyEInvoice(form.values.docType, form.values.docNo);
            
            // Convert base64 to Uint8Array for PdfViewer
            const binaryString = window.atob(res.pdf);
            const len = binaryString.length;
            const bytes = new Uint8Array(len);
            for (let i = 0; i < len; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }
            pdfData = bytes;
            
            toast.success("E-Invoice verified on NIC Portal!");
        } catch (err: any) {
            toast.error(err.message || "E-Invoice verification failed");
        } finally {
            verifying = false;
        }
    }

    const docTypeOptions = [
        { label: 'Invoice', value: 'Invoice' },
        { label: 'Credit Memo', value: 'CrNote' }
    ];

    const schema = $derived.by<FormSchema>(() => {
        return [
            {
                type: 'grid' as const,
                cols: 1,
                gap: 6,
                children: [
                    ...(showCenterSelect ? [{
                        type: 'custom' as const,
                        component: MasterSelect,
                        props: {
                            fieldName: 'respCenter',
                            masterType: 'respCenters',
                            label: 'Responsibility Center',
                            respCenterType: 'Sale',
                            singleSelect: true
                        },
                        required: true
                    }] : []),
                    {
                        type: 'field' as const,
                        name: 'docType',
                        label: 'Document Type',
                        inputType: 'select',
                        options: docTypeOptions,
                        required: true
                    },
                    {
                        type: 'custom' as const,
                        component: DocumentSelect,
                        props: {
                            fieldName: 'docNo',
                            label: 'Document Number',
                            docType: form.values.docType,
                            respCenter: form.values.respCenter
                        },
                        required: true
                    }
                ]
            }
        ];
    });

</script>

<svelte:head>
    <title>E-Invoice Check | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background pb-20">
    <PageHeading backHref="/" icon="shield-check">
        {#snippet title()}
            <span class="truncate">E-Invoice Portal Check</span>
        {/snippet}

        {#snippet description()}
            <div class="flex items-center gap-1.5 text-primary">
                <Icon name="external-link" class="size-3" />
                <span class="font-bold uppercase tracking-wider text-[10px]">Real-time NIC Portal Verification</span>
            </div>
        {/snippet}

        {#snippet actions()}
            <Button size="sm" onclick={handleVerify} disabled={verifying || !form.values.docNo} class="h-9 px-6 rounded-xl font-bold shadow-lg shadow-primary/20 transition-all active:scale-95">
                {#if verifying}
                    <Icon name="loader-circle" class="mr-2 h-4 w-4 animate-spin" />
                    Checking...
                {:else}
                    <Icon name="circle-check" class="mr-2 h-4 w-4" />
                    Verify on Portal
                {/if}
            </Button>
        {/snippet}
    </PageHeading>

    <div class="container mx-auto p-4 sm:p-6 lg:p-8 space-y-8">
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            <!-- Sidebar Selection -->
            <div class="lg:col-span-4 space-y-6">
                <div class="bg-card rounded-3xl border border-border/60 p-6 sm:p-8 shadow-2xl shadow-primary/5 relative overflow-hidden group">
                    <div class="absolute -top-24 -left-24 size-48 bg-primary/5 rounded-full blur-3xl group-hover:bg-primary/10 transition-colors duration-700"></div>
                    
                    <div class="relative">
                        <div class="flex items-center gap-3 mb-8">
                            <div class="size-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center shadow-inner">
                                <Icon name="file-search" class="h-5 w-5" />
                            </div>
                            <div>
                                <h3 class="text-sm font-bold uppercase tracking-widest text-muted-foreground/80">
                                    Document Selection
                                </h3>
                                <p class="text-[11px] text-muted-foreground font-medium">Verify IRN results from IRP</p>
                            </div>
                        </div>

                        <FormGenerator {form} {schema} root={true} />

                        <div class="mt-10 pt-6 border-t border-border/40">
                             <Button 
                                variant="secondary" 
                                onclick={handleVerify} 
                                disabled={verifying || !form.values.docNo}
                                class="w-full rounded-xl font-bold h-12 shadow-sm border border-border/50 hover:bg-primary/5 hover:text-primary transition-all duration-300"
                            >
                                {#if verifying}
                                    <Icon name="loader-circle" class="mr-2 h-4 w-4 animate-spin" />
                                    COMMUNICATING WITH PORTAL...
                                {:else}
                                    <Icon name="shield-check" class="mr-2 h-4 w-4" />
                                    VERIFY IRN STATUS
                                {/if}
                            </Button>
                        </div>
                    </div>
                </div>

                <!-- Info Card -->
                <div class="bg-slate-900 rounded-2xl p-5 text-white border border-white/5 shadow-xl">
                    <div class="flex gap-4">
                        <div class="size-10 rounded-full bg-blue-500/20 flex items-center justify-center shrink-0">
                            <Icon name="info" class="size-5 text-blue-400" />
                        </div>
                        <div>
                            <h4 class="font-bold text-sm">Security Note</h4>
                            <p class="text-xs text-white/50 mt-1 leading-relaxed">
                                Verification is performed securely via a session-aware headless browser on the server side. No credentials are exposed to the frontend.
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Result Viewer -->
            <div class="lg:col-span-8">
                {#if pdfData}
                    <div class="animate-in fade-in slide-in-from-bottom-4 duration-700 h-[calc(100vh-280px)] min-h-[600px] border border-border/60 rounded-3xl overflow-hidden shadow-2xl bg-card">
                        <PdfViewer data={pdfData} fileName={`${form.values.docNo}_Verified.pdf`} />
                    </div>
                {:else if verifying}
                    <div class="h-full min-h-[600px] flex flex-col items-center justify-center text-center p-12 border-2 border-dashed border-border/30 rounded-3xl bg-muted/10">
                        <div class="relative mb-12">
                            <div class="size-24 rounded-full border-4 border-primary/20 border-t-primary animate-spin"></div>
                            <div class="absolute inset-0 flex items-center justify-center">
                                <Icon name="browser" class="size-8 text-primary/40" />
                            </div>
                        </div>
                        <h4 class="font-bold text-xl text-foreground/80">Portal session active</h4>
                        <p class="text-sm text-muted-foreground mt-3 max-w-[320px] leading-relaxed">
                            A secure Puppeteer instance is verifying the document JSON on the NIC portal. This usually takes 10-15 seconds.
                        </p>
                    </div>
                {:else}
                    <div class="h-full min-h-[600px] flex flex-col items-center justify-center text-center p-12 border-2 border-dashed border-border/30 rounded-3xl bg-muted/10 transition-all duration-500 hover:border-primary/30 hover:bg-primary/5 group">
                        <div class="bg-background rounded-2xl p-6 mb-6 shadow-xl border border-border/50 group-hover:scale-110 transition-transform duration-500">
                            <Icon name="eye" class="h-10 w-10 text-muted-foreground/30 group-hover:text-primary/40" />
                        </div>
                        <h4 class="font-bold text-xl text-foreground/70">Ready to Review</h4>
                        <p class="text-sm text-muted-foreground mt-3 max-w-[280px] leading-relaxed">Select a document and click verify to start the real-time portal verification process.</p>
                        
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
