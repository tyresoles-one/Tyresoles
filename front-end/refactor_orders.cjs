const fs = require('fs');
let file = 'src/routes/(user)/(ecomile procurement)/ecoproc/orders/+page.svelte';
let content = fs.readFileSync(file, 'utf8');

content = content.replace(
    /import MasterList from "\$lib\/components\/venUI\/masterList\/MasterList\.svelte";/,
    `import { DataGrid, type DataGridColumn } from '$lib/components/venUI/datagrid';`
);

const columnsDef = `
  const columns: DataGridColumn<OrderInfo>[] = [
    { accessorKey: 'date', header: 'Date' },
    { accessorKey: 'orderNo', header: 'Order No' },
    { accessorKey: 'supplier', header: 'Supplier' },
    { accessorKey: 'location', header: 'Location' },
    { accessorKey: 'manager', header: 'Manager' },
    { accessorKey: 'qty', header: 'Qty' },
    { accessorKey: 'amount', header: 'Amount' }
  ];
</script>
`;

if (!content.includes('const columns: DataGridColumn')) {
    content = content.replace(/<\/script>/, columnsDef);
}

const masterListRegex = /<MasterList([\s\S]*?)<\/MasterList>/;

const dataGridReplacement = `<DataGrid
  title="Tyres Booking"
  description="Open Ecomile Procurement Orders"
  {items}
  {columns}
  {loading}
  bind:searchQuery
  mobileCardTitleKey="supplier"
  mobileCardSubtitleKey="orderNo"
  onRowClick={(item) => goto('/ecoproc/orders/' + item.orderNo)}
>
  {#snippet actions()}
    <Button
      variant="default"
      size="sm"
      class="gap-2 shrink-0"
      onclick={() => createNewOrder()}
      disabled={creatingOrder || loading}
    >
      <Icon name="plus" class="size-4 {creatingOrder ? 'animate-pulse' : ''}" />
      {creatingOrder ? "Creating…" : "New Order"}
    </Button>
  {/snippet}
</DataGrid>`;

content = content.replace(masterListRegex, dataGridReplacement);

fs.writeFileSync(file, content);
console.log('Successfully refactored orders list to DataGrid');
