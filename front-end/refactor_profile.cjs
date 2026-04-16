const fs = require('fs');
let file = 'src/routes/(user)/profile/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

// Add imports for DetailItem and LoadingButton
content = content.replace(
    /import \{ Separator \} from '\$lib\/components\/ui\/separator';/,
    `import { Separator } from '$lib/components/ui/separator';\n\timport DetailItem from '$lib/components/venUI/detail-item/DetailItem.svelte';\n\timport AsyncButton from '$lib/components/venUI/button/AsyncButton.svelte';`
);

// Replace "DetailItem" pattern in System Information
content = content.replace(
    /<div class="flex flex-col gap-1">\s*<span class="text-xs font-medium text-muted-foreground">([^<]+)<\/span>\s*(<span [^>]+>[\s\S]*?<\/span>)\s*<\/div>/g,
    (match, label, valueSpan) => {
        return `<DetailItem label="${label}">\n\t\t\t\t\t\t\t\t\t${valueSpan}\n\t\t\t\t\t\t\t\t</DetailItem>`;
    }
);

content = content.replace(
    /<div class="flex flex-col gap-1">\s*<span class="text-xs font-medium text-muted-foreground">([^<]+)<\/span>\s*([\s\S]*?)<\/div>/g,
    (match, label, inner) => {
        if(inner.includes('<Input')) {
            return `<DetailItem label="${label}">\n\t\t\t\t\t\t\t\t\t${inner}\n\t\t\t\t\t\t\t\t</DetailItem>`;
        }
        return match;
    }
);


// Refactor Save Buttons to AsyncButton
content = content.replace(
    /<Button onclick=\{handleSave\} disabled=\{loading \|\| saving\} class="min-w-\[120px\] transition-all hover:shadow-md">\s*\{#if saving\}\s*<Icon name="loader-2" class="mr-2 size-4 animate-spin" \/>\s*Saving\.\.\.\s*\{:else\}\s*Save Changes\s*\{\/if\}\s*<\/Button>/g,
    `<AsyncButton onclick={handleSave} loading={saving} disabled={loading} loadingText="Saving..." class="min-w-[120px] transition-all hover:shadow-md">Save Changes</AsyncButton>`
);

fs.writeFileSync(file, content);
console.log('Profile refactored');
