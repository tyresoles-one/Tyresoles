/**
 * PDF print service: renders each PDF page to an image in a hidden iframe and
 * calls window.print() from that document (avoids cross-origin frame issues).
 *
 * @page size follows the PDF’s natural orientation so landscape reports print
 * correctly. Page breaks are only between sheets (not after the last page) to
 * avoid blank even pages when the user changes orientation in the print dialog.
 */

const BLOB_REVOKE_DELAY_MS = 60000;

function escapeAttr(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/"/g, '&quot;');
}

/**
 * Prints a PDF document by rendering each page to a canvas and then to an image.
 * @param pdfDoc The PDF.js document object
 */
export async function printPdfDocument(pdfDoc: any): Promise<void> {
  const images: string[] = [];

  // Match print @page orientation to the document (use first page; typical for reports)
  const firstPage = await pdfDoc.getPage(1);
  const firstVp = firstPage.getViewport({ scale: 1 });
  const isLandscape = firstVp.width >= firstVp.height;

  for (let i = 1; i <= pdfDoc.numPages; i++) {
    const page = await pdfDoc.getPage(i);
    const viewport = page.getViewport({ scale: 2 });
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');

    if (!context) continue;

    canvas.height = viewport.height;
    canvas.width = viewport.width;

    await page
      .render({
        canvasContext: context,
        viewport: viewport,
      })
      .promise;

    images.push(canvas.toDataURL('image/png'));
  }

  /* Default A4 is portrait; explicit landscape when PDF is wider than tall */
  const pageSizeCss = isLandscape ? 'A4 landscape' : 'A4';

  const iframe = document.createElement('iframe');
  iframe.style.position = 'absolute';
  iframe.style.top = '-10000px';
  iframe.style.left = '-10000px';
  iframe.style.border = '0';
  /* Give the iframe a real A4-sized layout box so print layout is not collapsed */
  if (isLandscape) {
    iframe.style.width = '1123px';
    iframe.style.height = '794px';
  } else {
    iframe.style.width = '794px';
    iframe.style.height = '1123px';
  }
  document.body.appendChild(iframe);

  const doc = iframe.contentWindow?.document;
  if (!doc) {
    document.body.removeChild(iframe);
    throw new Error('Failed to access iframe document');
  }

  const imgBox = isLandscape
    ? 'width: 297mm; height: 210mm;'
    : 'width: 210mm; height: 297mm;';

  const sheetsHtml = images.map((src) => `<img src="${src}" alt="" />`).join('');

  const html = `
    <!DOCTYPE html>
    <html>
      <head>
        <title>Print</title>
        <style>
          @page {
            size: ${pageSizeCss};
            margin: 0;
          }
          html, body {
            margin: 0;
            padding: 0;
          }
          /* One image per sheet: A4 mm box, page-break-before avoids duplicate blank pages in Chromium */
          img {
            display: block;
            ${imgBox}
            object-fit: contain;
            box-sizing: border-box;
          }
          img:not(:first-child) {
            page-break-before: always;
          }
        </style>
      </head>
      <body>
        ${sheetsHtml}
        <script>
          window.focus();
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
  const PRINT_DELAY_MS = 3500;
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
    `<!DOCTYPE html><html><head><style>@page { margin: 0; }</style></head><body style="margin:0"><embed src="${escapeAttr(pdfBlobUrl)}" type="application/pdf" style="width:100%;height:100%" /><script>setTimeout(function(){ window.focus(); window.print(); }, ${PRINT_DELAY_MS});<\/script></body></html>`,
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
