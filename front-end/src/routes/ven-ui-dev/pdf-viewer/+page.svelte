<script lang="ts">
	import { PdfViewer } from '$lib/components/venUI/pdf-viewer';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Label } from '$lib/components/ui/label';

	let pdfUrl = $state('https://raw.githubusercontent.com/mozilla/pdf.js/ba2edeae/examples/learning/helloworld.pdf');
    let pdfData = $state<ArrayBuffer | null>(null);
    let fileName = $state('document.pdf');
    
	let inputUrl = $state(pdfUrl);
    let isLoadingData = $state(false);

	function updateUrl() {
        // Clear data mode when using URL
		pdfUrl = inputUrl;
        pdfData = null; 
        fileName = inputUrl.split('/').pop() || 'document.pdf';
	}

	function loadSample(type: 'simple' | 'complex') {
		if (type === 'simple') {
			inputUrl = 'https://raw.githubusercontent.com/mozilla/pdf.js/ba2edeae/examples/learning/helloworld.pdf';
		} else {
			// Using a slightly larger/more complex PDF for demonstration (Trace-based JIT compilation for PDF.js)
			inputUrl = 'https://mozilla.github.io/pdf.js/web/compressed.tracemonkey-pldi-09.pdf';
		}
		updateUrl();
	}

    async function loadFromBackendSim() {
        try {
            isLoadingData = true;
            // simulate a backend call by fetching the simple PDF as arraybuffer
            const res = await fetch('https://raw.githubusercontent.com/mozilla/pdf.js/ba2edeae/examples/learning/helloworld.pdf');
            if (!res.ok) throw new Error('Failed to fetch');
            
            const buffer = await res.arrayBuffer();
            
            // Switch to data mode
            pdfData = buffer;
            pdfUrl = ''; // Clear URL to ensure we use data
            fileName = 'backend-generated-report.pdf'; // Custom filename
            inputUrl = ''; // Clear input to show we are in data mode
            
        } catch (e) {
            console.error(e);
            alert('Failed to simulated backend load');
        } finally {
            isLoadingData = false;
        }
    }
</script>

<div class="container mx-auto p-4 space-y-6">
  <div class="flex flex-col space-y-2">
    <h1 class="text-3xl font-bold tracking-tight">PDF Viewer Component</h1>
    <p class="text-muted-foreground">
      A responsive, feature-rich PDF viewer built on top of pdf.js with
      mobile-first design. Supports both URL and Memory Stream (ArrayBuffer)
      sources.
    </p>
  </div>

  <div class="flex flex-col gap-4 p-4 border rounded-lg bg-card">
    <div class="space-y-2">
      <Label>Load via URL</Label>
      <div class="flex gap-2">
        <Input
          bind:value={inputUrl}
          placeholder="Enter PDF URL..."
          class="flex-1"
        />
        <Button onclick={updateUrl}>Load URL</Button>
      </div>
    </div>

    <div class="flex flex-wrap gap-2">
      <Button variant="outline" size="sm" onclick={() => loadSample("simple")}
        >Load Simple URL</Button
      >
      <Button variant="outline" size="sm" onclick={() => loadSample("complex")}
        >Load Complex URL</Button
      >
      <div class="w-px h-6 bg-border mx-2"></div>
      <Button
        variant="secondary"
        size="sm"
        onclick={loadFromBackendSim}
        disabled={isLoadingData}
      >
        {isLoadingData ? "Loading..." : "Simulate Backend Stream (Binary Data)"}
      </Button>
    </div>

    {#if pdfData}
      <div class="text-xs text-muted-foreground bg-secondary/20 p-2 rounded">
        <strong>Active Mode:</strong> Binary Data Stream ({pdfData.byteLength} bytes)
        | <strong>Filename:</strong>
        {fileName}
      </div>
    {:else}
      <div class="text-xs text-muted-foreground bg-secondary/20 p-2 rounded">
        <strong>Active Mode:</strong> URL Source
      </div>
    {/if}
  </div>

  <div
    class="h-[800px] border rounded-lg overflow-hidden bg-background shadow-sm"
  >
    <PdfViewer url={pdfUrl} data={pdfData} {fileName} />
  </div>
</div>
