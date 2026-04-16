const fs = require('fs');
let file = 'src/routes/(user)/(masters)/dealers/[id]/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

// A generic replacer for this exact pattern
const pattern = /<div class="space-y-1\.5">\s*<label for="([^"]+)" class="[^"]+">\s*(?:<Icon name="([^"]+)" class="[^"]+" \/>\s*)?([^<\n]+)\s*(?:<span class="text-destructive" aria-hidden="true">\*<\/span>\s*)?<\/label>\s*([\s\S]*?)(?:\{#if fieldErrors\.([a-zA-Z0-9_]+)\}\s*<p id="[^"]+" class="[^"]+" role="alert">\{fieldErrors\.[a-zA-Z0-9_]+\}<\/p>\s*\{\/if\}\s*)?<\/div>/g;

content = content.replace(pattern, (match, id, icon, labelText, innerContent, errorKey) => {
    let lines = innerContent.trimEnd();
    let reqStr = match.includes('<span class="text-destructive"') ? ' required' : '';
    let iconStr = icon ? ` icon="${icon}"` : '';
    let errStr = errorKey ? ` error={fieldErrors.${errorKey}}` : '';
    let label = labelText.trim();
    
    return `<FormField id="${id}" label="${label}"${iconStr}${reqStr}${errStr}>\n\t${lines.trim()}\n</FormField>`;
});

fs.writeFileSync(file, content);
console.log('done');
