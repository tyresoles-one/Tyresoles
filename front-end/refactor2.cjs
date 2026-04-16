const fs = require('fs');
let file = 'src/routes/(user)/(masters)/dealers/[id]/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

const pattern = /<div class="space-y-1\.5(?: flex items-end)?">\s*<label[^>]*>\s*(?:<Icon name="([^"]+)" class="[^"]+" \/>\s*)?([^<\n]+)\s*(?:<span class="text-destructive" aria-hidden="true">\*<\/span>\s*)?<\/label>\s*([\s\S]*?)(?:\{#if fieldErrors\.([a-zA-Z0-9_]+)\}\s*<p [^>]+>\{fieldErrors\.[a-zA-Z0-9_]+\}<\/p>\s*\{\/if\}\s*)?<\/div>/g;

content = content.replace(pattern, (match, icon, labelText, innerContent, errorKey) => {
    // If it's the switch for branded shop, let's keep it as is, it's a bit too custom.
    if (innerContent.includes('<Switch')) return match;
    
    let lines = innerContent.trimEnd();
    let reqStr = match.includes('<span class="text-destructive"') ? ' required' : '';
    let iconStr = icon ? ` icon="${icon}"` : '';
    let errStr = errorKey ? ` error={fieldErrors.${errorKey}}` : '';
    let label = labelText.trim();
    
    return `<FormField label="${label}"${iconStr}${reqStr}${errStr}>\n\t${lines.trim()}\n</FormField>`;
});

fs.writeFileSync(file, content);
console.log('done stage 2');
