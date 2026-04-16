const fs = require('fs');
let file = 'src/routes/(user)/(masters)/vendors/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

// Replace standard imports
content = content.replace(
    /import MasterList from '\$lib\/components\/venUI\/masterList\/MasterList\.svelte';/,
    `import { DataGrid, type DataGridColumn } from '$lib/components/venUI/datagrid';`
);

// Define columns right before `</script>`
const columnsDef = `
const columns: DataGridColumn<Vendor>[] = [
{ accessorKey: 'code', header: 'Code' },
{ accessorKey: 'name', header: 'Full Name' },
{ accessorKey: 'depot', header: 'Location' },
{ accessorKey: 'dealershipName', header: 'Dealership Name' },
{ accessorKey: 'eMail', header: 'Email' },
{ accessorKey: 'phoneNo', header: 'Phone' }
];
</script>
`;
if (!content.includes('const columns: DataGridColumn')) {
    content = content.replace(/<\/script>/, columnsDef);
}

// Replace MasterList markup entirely
const masterListRegex = /<MasterList([\s\S]*?)<\/MasterList>/;

const dataGridReplacement = `<DataGrid
title="Vendors"
description="View and manage vendors"
items={list.items}
{columns}
pagination={list.pagination}
loading={list.loading}
loadingMore={list.loadingMore}
bind:searchQuery={list.searchQuery.value}
mobileCardTitleKey="name"
mobileCardSubtitleKey="code"
onRowClick={(vendor) => goto(vendorDetailPath(vendor))}
>
{#snippet actions()}
<Button
size="sm"
class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
>
<Icon name="plus" class="size-3.5" />
<span class="hidden sm:inline">Add Vendor</span>
<span class="sm:hidden">Add</span>
</Button>
{/snippet}
</DataGrid>`;

content = content.replace(masterListRegex, dataGridReplacement);

fs.writeFileSync(file, content);
console.log('Successfully refactored vendors list to DataGrid');
