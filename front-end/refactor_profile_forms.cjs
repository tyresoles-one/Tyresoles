const fs = require('fs');
let file = 'src/routes/(user)/profile/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

// Add import if missing
if (!content.includes('FormField.svelte')) {
    content = content.replace(
        /import DetailItem/,
        `import FormField from '$lib/components/venUI/form/FormField.svelte';\n\timport DetailItem`
    );
}

// Replace space-y-2 wrappers
const pattern = /<div class="space-y-2">\s*<Label for="([^"]+)">([^<]+)<\/Label>\s*<div class="relative">\s*<Icon name="([^"]+)" class="[^"]+" \/>\s*(<Input[^>]+>)\s*<\/div>\s*<\/div>/g;

content = content.replace(pattern, (match, id, label, icon, inputTag) => {
    return `<FormField id="${id}" label="${label}" icon="${icon}">\n\t\t\t\t\t\t\t\t\t${inputTag}\n\t\t\t\t\t\t\t\t</FormField>`;
});

fs.writeFileSync(file, content);
console.log('Profile forms refactored');
