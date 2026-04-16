const fs = require('fs');
let file = 'src/routes/(user)/(masters)/dealers/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

// Replace standard imports
content = content.replace(
    /import MasterList from '\$lib\/components\/venUI\/masterList\/MasterList\.svelte';/,
    `import { DataGrid, type DataGridColumn } from '$lib/components/venUI/datagrid';`
);

// Define columns right before `</script>`
const columnsDef = `
const columns: DataGridColumn<Dealer>[] = [
{ accessorKey: 'code', header: 'Code' },
{ accessorKey: 'name', header: 'Full Name' },
{ accessorKey: 'depot', header: 'Location' },
{ accessorKey: 'dealershipName', header: 'Dealership Name' },
{ accessorKey: 'eMail', header: 'Email' },
{ accessorKey: 'phoneNo', header: 'Phone' }
];
</script>
`;
content = content.replace(/<\/script>/, columnsDef);

// Replace MasterList markup entirely
const masterListRegex = /<div class="min-h-screen bg-background pb-20">\s*<MasterList([\s\S]*?)<\/MasterList>\s*<\/div>/;

const dataGridReplacement = `<div class="min-h-screen bg-background pb-20 pt-8">
<DataGrid
title="Dealers"
description="View and manage dealers"
items={list.items}
{columns}
pagination={list.pagination}
loading={list.loading}
loadingMore={list.loadingMore}
bind:searchQuery={list.searchQuery.value}
mobileCardTitleKey="name"
mobileCardSubtitleKey="code"
onRowClick={(dealer) => goto(dealerDetailPath(dealer))}
>
{#snippet actions()}
{#if canAddDealer}
<Button
size="sm"
class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
onclick={() => (addDialogOpen = true)}
>
<Icon name="plus" class="size-3.5" />
<span class="hidden sm:inline">Add Dealer</span>
<span class="sm:hidden">Add</span>
</Button>
{/if}
{/snippet}
</DataGrid>
</div>`;

content = content.replace(masterListRegex, dataGridReplacement);

fs.writeFileSync(file, content);
console.log('Successfully refactored dealers list to DataGrid');
