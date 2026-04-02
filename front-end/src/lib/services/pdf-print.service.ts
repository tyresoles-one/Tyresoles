/**
 * PDF print service: opens a same-origin window with the PDF embedded and
 * calls window.print() from inside that window (never from the parent) to avoid
 * SecurityError: "Blocked a frame with origin ... from accessing a cross-origin frame".
 */

const PRINT_DELAY_MS = 3500;
const BLOB_REVOKE_DELAY_MS = 60000;

function escapeAttr(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/"/g, '&quot;');
}

/**
 * Prints a PDF document by rendering each page to a canvas and then to an image.
 * This is the most reliable production-ready method as it avoids browser PDF plugin
 * security and rendering quirks.
 * @param pdfDoc The PDF.js document object
 */
export async function printPdfDocument(pdfDoc: any): Promise<void> {
  const images: string[] = [];
  
  // Render each page to a data URL
  for (let i = 1; i <= pdfDoc.numPages; i++) {
    const page = await pdfDoc.getPage(i);
    const viewport = page.getViewport({ scale: 2 }); // High quality for printing
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    
    if (!context) continue;
    
    canvas.height = viewport.height;
    canvas.width = viewport.width;
    
    await page.render({
      canvasContext: context,
      viewport: viewport
    }).promise;
    
    images.push(canvas.toDataURL('image/png'));
  }

  // Create a hidden iframe for printing
  const iframe = document.createElement('iframe');
  iframe.style.position = 'absolute';
  iframe.style.top = '-10000px';
  iframe.style.left = '-10000px';
  iframe.style.border = '0';
  document.body.appendChild(iframe);

  const doc = iframe.contentWindow?.document;
  if (!doc) {
    document.body.removeChild(iframe);
    throw new Error('Failed to access iframe document');
  }

  // Generate HTML with images
  const html = `
    <!DOCTYPE html>
    <html>
      <head>
        <title>Print</title>
        <style>
          /* Hide default browser header & footer when printing */
          @page { margin: 0; }
          body { margin: 0; padding: 0; }
          img { 
            display: block; 
            width: 100%; 
            height: auto; 
            page-break-after: always; 
          }
          @media print {
            body { margin: 0; }
            img { width: 100%; height: auto; }
          }
        </style>
      </head>
      <body>
        ${images.map(src => `<img src="${src}" />`).join('')}
        <script>
          window.focus();
          // Small delay to ensure images are layouted
          setTimeout(function() {
            window.print();
          }, 500);
        <\/script>
      </body>
    </html>
  `;

  doc.open();
  doc.write(html);
  doc.close();

  // Cleanup after a while
  setTimeout(() => {
    if (document.body.contains(iframe)) {
      document.body.removeChild(iframe);
    }
  }, BLOB_REVOKE_DELAY_MS);
}

/**
 * @deprecated Use printPdfDocument for high-reliability printing.
 */
export function printPdfBlob(blob: Blob): HTMLIFrameElement | null {
  const pdfBlobUrl = URL.createObjectURL(blob);
  const iframe = document.createElement('iframe');
  iframe.style.position = 'absolute';
  iframe.style.top = '-10000px';
  iframe.style.left = '-10000px';
  iframe.style.width = '1000px';
  iframe.style.height = '1000px';
  iframe.style.border = '0';
  document.body.appendChild(iframe);

  const doc = iframe.contentWindow?.document;
  if (!doc) {
    URL.revokeObjectURL(pdfBlobUrl);
    document.body.removeChild(iframe);
    return null;
  }

  doc.open();
  doc.write(
    `<!DOCTYPE html><html><head><style>@page { margin: 0; }</style></head><body style="margin:0"><embed src="${escapeAttr(pdfBlobUrl)}" type="application/pdf" style="width:100%;height:100%" /><script>setTimeout(function(){ window.focus(); window.print(); }, ${PRINT_DELAY_MS});<\/script></body></html>`
  );
  doc.close();

  setTimeout(() => {
    if (document.body.contains(iframe)) {
      document.body.removeChild(iframe);
    }
    URL.revokeObjectURL(pdfBlobUrl);
  }, BLOB_REVOKE_DELAY_MS);

  return iframe;
}
