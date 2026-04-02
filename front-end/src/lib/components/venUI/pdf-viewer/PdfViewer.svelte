<script lang="ts">
	import { onMount, onDestroy, untrack } from 'svelte';
	// Removed static pdfjs imports to avoid SSR issues
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Alert, AlertDescription, AlertTitle } from '$lib/components/ui/alert';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { printPdfDocument } from '$lib/services/pdf-print.service';
	import { cn } from '$lib/utils'; 

	let { 
        url = '', 
        data = null, 
        fileName = 'document.pdf',
        class: className = '' 
    } = $props<{
        url?: string;
        data?: Uint8Array | ArrayBuffer | null;
        fileName?: string;
        class?: string;
    }>();

    // Use any for pdfjs types since we are importing dynamically
	let pdfjs: any = $state(null);
	let pdfDoc = $state<any>(null); // Type as any for now
	let pageNum = $state(1);
	let numPages = $state(0);
	let scale = $state(1.0);
	let rotation = $state(0);
	let canvasRef = $state<HTMLCanvasElement | null>(null);
	let containerRef = $state<HTMLDivElement | null>(null);
	let loading = $state(true);
	let printing = $state(false);
	let error = $state<string | null>(null);
    
    // Internal state (non-reactive)
	let renderTask: any = null;
	let loadingTask: any = null;
    
	let resizeObserver: ResizeObserver | null = null;
	let resizeTimeout: ReturnType<typeof setTimeout> | null = null;

    onMount(async () => {
        try {
            const pdfjsDist = await import('pdfjs-dist');
            const workerModule = await import('pdfjs-dist/build/pdf.worker.mjs?url');
            const pdfjsWorker = workerModule?.default ?? workerModule;
            if (!pdfjsWorker || typeof pdfjsWorker !== 'string') {
                throw new Error('PDF worker URL not found');
            }
            pdfjs = pdfjsDist;
            pdfjs.GlobalWorkerOptions.workerSrc = pdfjsWorker;
        } catch (e: any) {
            console.error("Failed to load pdfjs-dist", e);
            error = "Failed to initialize PDF viewer: " + (e?.message ?? String(e));
            loading = false;
        }
    });

	onDestroy(() => {
		cleanup();
	});

	function cleanup() {
		if (resizeObserver) {
			resizeObserver.disconnect();
			resizeObserver = null;
		}
		if (resizeTimeout) {
			clearTimeout(resizeTimeout);
			resizeTimeout = null;
		}
		if (loadingTask) {
			loadingTask.destroy();
			loadingTask = null;
		}
		if (renderTask) {
			renderTask.cancel();
			renderTask = null;
		}
	}

	// Load PDF when URL or Data changes (only if pdfjs is loaded)
	$effect(() => {
        const targetUrl = url;
        const targetData = data;
        const driver = pdfjs;
        
		if ((targetUrl || targetData) && driver) {
            untrack(() => {
			    loadPdf(targetUrl || targetData);
            });
		}
	});

	// Render page when relevant state changes
	$effect(() => {
        // Read reactive variables to track dependencies
        const _deps = [pdfDoc, pageNum, scale, rotation, canvasRef]; 
		if (pdfDoc && pageNum && canvasRef !== null) {
            untrack(() => {
			    renderPage(pageNum);
            });
		}
	});

    // Setup resize observer
	$effect(() => {
		if (containerRef && !resizeObserver) {
			resizeObserver = new ResizeObserver((entries) => {
				if (resizeTimeout) clearTimeout(resizeTimeout);
				resizeTimeout = setTimeout(() => {
					if (pdfDoc) {
                        fitToWidth();
                    }
				}, 200);
			});
			resizeObserver.observe(containerRef);
		}
	});

	async function loadPdf(src: string | Uint8Array | ArrayBuffer | any) {
        if (!pdfjs) {
            return;
        }

        // Create a unique identifier for this load attempt (via the task object itself)
        let currentTask: any = null;

		try {
			loading = true;
			error = null;
            
            // Cancel previous global task
            if (loadingTask) {
                loadingTask.destroy();
                loadingTask = null;
            }
            if (renderTask) {
                renderTask.cancel();
                renderTask = null;
            }
            
            // Reset state
            pdfDoc = null;
            numPages = 0;
            pageNum = 1;

            if (typeof src === 'string') {
                 currentTask = pdfjs.getDocument(src);
            } else {
                 // Clone so pdf.js worker doesn't detach the original (avoids DataCloneError on re-run)
                 const copy = src instanceof ArrayBuffer ? src.slice(0) : (src as Uint8Array).slice(0);
                 currentTask = pdfjs.getDocument(copy);
            }
            
            loadingTask = currentTask;
            
			const doc = await currentTask.promise;

            if (loadingTask !== currentTask) return;

            // Calculate initial scale
            try {
               const page = await doc.getPage(1);
               const viewport = page.getViewport({ scale: 1 });
               
               if (containerRef) {
                    const clientWidth = containerRef.clientWidth;
                    if (clientWidth > 0) {
                        const containerWidth = clientWidth - 48;
                        if (containerWidth > 0) {
                            const newScale = containerWidth / viewport.width;
                            scale = newScale;
                        }
                    }
               }
            } catch (e) {
                console.warn("Failed to calculate initial scale:", e);
            }
			
            if (loadingTask !== currentTask) return;

			pdfDoc = doc;
			numPages = doc.numPages;
            
            // Note: we do NOT reset pageNum here to 1 again, as we set it at start.
			
		} catch (err: any) {
            if (loadingTask !== currentTask) return; // Ignore errors from cancelled tasks
            
            // Ignore specific cancellation errors even if we think we are active
            if (err.name === 'RenderingCancelledException' || err.message === 'Loading aborted' || err.message === 'Worker was destroyed') {
                return;
            }
            
			console.error('Error loading PDF:', err);
			error = err.message || 'Failed to load PDF';
		} finally {
            // Only turn off loading if WE are still the active task
            if (loadingTask === currentTask) {
			    loading = false;
            }
		}
	}

	async function renderPage(num: number) {
		if (!pdfDoc || !canvasRef) return;

		try {
			// If a render task is pending, cancel it
			if (renderTask) {
				await renderTask.cancel();
                renderTask = null;
			}

			const page = await pdfDoc.getPage(num);
			
			const viewport = page.getViewport({ scale, rotation });
            
            // Re-check canvasRef as it might have become null during await
            if (!canvasRef) return;
            
			const canvas = canvasRef;
			const context = canvas.getContext('2d');

			if (!context) return;

			// Handle high DPI screens
			const outputScale = window.devicePixelRatio || 1;

			canvas.width = Math.floor(viewport.width * outputScale);
			canvas.height = Math.floor(viewport.height * outputScale);
			canvas.style.width = Math.floor(viewport.width) + "px";
			canvas.style.height = Math.floor(viewport.height) + "px";

			const transform = outputScale !== 1
				? [outputScale, 0, 0, outputScale, 0, 0]
				: null;

			const renderContext = {
				canvasContext: context,
				transform: transform,
				viewport: viewport
			};

			renderTask = page.render(renderContext);
			await renderTask.promise;
			renderTask = null;
		} catch (err: any) {
			if (err.name === 'RenderingCancelledException') {
				// Rendering cancelled, expected behavior when switching pages fast
				return;
			}
			console.error('Error rendering page:', err);
		}
	}

	function changePage(delta: number) {
		if (pdfDoc && pageNum + delta >= 1 && pageNum + delta <= numPages) {
			pageNum += delta;
		}
	}

	function setPage(num: number) {
		if (num >= 1 && num <= numPages) {
			pageNum = num;
		}
	}

	function zoomIn() {
		scale = Math.min(scale + 0.25, 3.0);
	}

	function zoomOut() {
		scale = Math.max(scale - 0.25, 0.5);
	}

	function rotate() {
		rotation = (rotation + 90) % 360;
	}

	function fitToWidth() {
		if (!containerRef || !pdfDoc) return;
        // Don't run if container has no width yet
        if (containerRef.clientWidth === 0) return;

		// A rough estimate, getting first page to calculate aspect ratio
		pdfDoc.getPage(1).then((page: any) => {
			const viewport = page.getViewport({ scale: 1, rotation });
			const containerWidth = containerRef!.clientWidth - 48; // minus padding
			if (containerWidth > 0) {
				const newScale = containerWidth / viewport.width;
                // Add epsilon check to prevent infinite loops (0.01 difference required)
                if (Math.abs(newScale - scale) > 0.01) {
				    scale = newScale;
                }
			}
		});
	}

    async function downloadPdf() {
        if (!pdfDoc) return;
        try {
            const data = await pdfDoc.getData();
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            
            const link = document.createElement('a');
            link.href = blobUrl;
            link.download = fileName || url?.split('/').pop() || 'document.pdf';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(blobUrl);
        } catch (e) {
            console.error('Download failed', e);
            // Fallback
            if (url) {
                window.open(url, '_blank');
            }
        }
    }

    async function printPdf() {
        if (!pdfDoc) {
            toast.error('PDF not ready to print');
            return;
        }

        if (printing) return;

        try {
            printing = true;
            await printPdfDocument(pdfDoc);
            printing = false;
        } catch (e) {
            console.error('Print failed', e);
            toast.error('Print failed');
            printing = false;
        }
    }

	function handleKeyDown(e: KeyboardEvent) {
		if (e.key === 'ArrowRight') changePage(1);
		if (e.key === 'ArrowLeft') changePage(-1);
	}
</script>

<svelte:window onkeydown={handleKeyDown} />

<div
  class={cn("flex flex-col gap-4 w-full h-full relative", className)}
  bind:this={containerRef}
>
  <!-- Toolbar -->
  <div
    class="flex flex-nowrap items-center justify-between gap-2 p-2 bg-background border rounded-lg shadow-sm sticky top-0 z-10 w-full overflow-x-auto"
  >
    <div class="flex items-center gap-1 shrink-0">
      <Button
        variant="ghost"
        size="icon"
        onclick={() => changePage(-1)}
        disabled={pageNum <= 1 || loading}
      >
        <Icon name="ChevronLeft" class="size-4" />
      </Button>
      <div class="flex items-center gap-1 mx-1 sm:gap-2 sm:mx-2">
        <Input
          type="number"
          min="1"
          max={numPages}
          value={pageNum}
          onchange={(e) => setPage(parseInt(e.currentTarget.value))}
          class="w-10 h-8 text-center p-0 sm:w-16"
          disabled={loading}
        />
        <span class="text-xs text-muted-foreground whitespace-nowrap"
          >/ {numPages}</span
        >
      </div>
      <Button
        variant="ghost"
        size="icon"
        onclick={() => changePage(1)}
        disabled={pageNum >= numPages || loading}
      >
        <Icon name="ChevronRight" class="size-4" />
      </Button>
    </div>

    <div class="flex items-center gap-1 overflow-x-auto">
      <Button
        variant="ghost"
        size="icon"
        onclick={zoomOut}
        disabled={loading}
        title="Zoom Out"
      >
        <Icon name="ZoomOut" class="size-4" />
      </Button>
      <span
        class="text-xs text-muted-foreground w-12 text-center hidden sm:block"
        >{Math.round(scale * 100)}%</span
      >
      <Button
        variant="ghost"
        size="icon"
        onclick={zoomIn}
        disabled={loading}
        title="Zoom In"
      >
        <Icon name="ZoomIn" class="size-4" />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        onclick={rotate}
        disabled={loading}
        title="Rotate"
        class="hidden sm:inline-flex"
      >
        <Icon name="RotateCw" class="size-4" />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        onclick={fitToWidth}
        disabled={loading}
        title="Fit Width"
        class="hidden sm:inline-flex"
      >
        <Icon name="Maximize" class="size-4" />
      </Button>

      <!-- Separator using margin instead of div to avoid hydration issues -->
      <div class="ml-2 flex items-center gap-1 border-l pl-2">
        <Button
          variant="ghost"
          size="icon"
          onclick={downloadPdf}
          disabled={loading}
          title="Download"
        >
          <Icon name="Download" class="size-4" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          onclick={printPdf}
          disabled={loading || printing}
          title="Print"
        >
          {#if printing}
            <Icon name="loader-circle" class="size-4 animate-spin text-primary" />
          {:else}
            <Icon name="Printer" class="size-4" />
          {/if}
        </Button>
      </div>
    </div>
  </div>

  <!-- Content -->
  <div
    class="flex-1 w-full overflow-auto flex justify-center p-4 bg-muted/20 rounded-lg border min-h-[400px]"
  >
    {#if loading}
      <div
        class="flex flex-col items-center justify-center space-y-4 w-full h-full"
      >
        <Icon name="loader-circle" class="size-8 animate-spin text-primary" />
        <div class="space-y-2 w-full max-w-md">
          <Skeleton class="h-[600px] w-full" />
        </div>
      </div>
    {:else if error}
      <div class="flex items-center justify-center w-full h-full">
        <Alert variant="destructive" class="max-w-md">
          <Icon name="CircleAlert" class="h-4 w-4" />
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      </div>
    {:else}
      <canvas bind:this={canvasRef} class="shadow-lg max-w-full"></canvas>
    {/if}
  </div>
</div>
