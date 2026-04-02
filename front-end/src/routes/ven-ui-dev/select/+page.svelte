<script lang="ts">
	import { Select } from "$lib/components/venUI/select";
	import * as Card from "$lib/components/ui/card";
	import { Separator } from "$lib/components/ui/separator";

	// Example 1: Simple String Array
	const fruits = ["Apple", "Banana", "Cherry", "Date", "Elderberry"];
	let selectedFruit = $state("Apple");

	// Example 2: Object Array
    type User = { id: string; name: string; email: string; role: string };
	const users: User[] = [
		{ id: "1", name: "John Doe", email: "john@example.com", role: "Admin" },
		{ id: "2", name: "Jane Smith", email: "jane@example.com", role: "User" },
		{ id: "3", name: "Bob Johnson", email: "bob@example.com", role: "Manager" },
		{ id: "4", name: "Alice Brown", email: "alice@example.com", role: "User" },
		{ id: "5", name: "Charlie Wilson", email: "charlie@example.com", role: "User" },
	];
	let selectedUserId = $state("2");

	// Example 3: Table View
	let selectedUserTableId = $state(null);

	const userColumns: { header: string; accessor: keyof User | ((item: User) => any); class?: string }[] = [
		{ header: "Name", accessor: "name", class: "font-medium min-w-[150px]" },
		{ header: "Email", accessor: "email", class: "min-w-[200px]" },
		{ header: "Role", accessor: "role", class: "text-muted-foreground w-20 min-w-[100px]" },
	];
    
    // Example 4: Complex Data
    type Product = { id: number; code: string; name: string; category: string; price: number; stock: number };
    const products: Product[] = [
        { id: 101, code: "P-101", name: "Wireless Mouse", category: "Electronics", price: 29.99, stock: 150 },
        { id: 102, code: "P-102", name: "Mechanical Keyboard", category: "Electronics", price: 89.99, stock: 45 },
        { id: 103, code: "P-103", name: "HD Monitor 24\"", category: "Electronics", price: 159.99, stock: 12 },
        { id: 104, code: "P-104", name: "USB-C Hub", category: "Accessories", price: 34.50, stock: 85 },
        { id: 105, code: "P-105", name: "Ergonomic Chair", category: "Furniture", price: 249.00, stock: 5 },
    ];
    let selectedProduct = $state(null);

    const productColumns: { header: string; accessor: keyof Product | ((item: Product) => any); class?: string }[] = [
        { header: "Code", accessor: "code", class: "w-16 font-mono text-xs min-w-[80px]" },
        { header: "Product Name", accessor: "name", class: "font-medium min-w-[150px]" },
        { header: "Category", accessor: "category", class: "text-muted-foreground min-w-[120px]" },
        { header: "Price", accessor: (item: Product) => `$${item.price.toFixed(2)}`, class: "text-right w-16" },
        { header: "Stock", accessor: "stock", class: "text-center w-12" },
    ];

    // Example 5: Multiple Selection
    let selectedMultipleIds: string[] = $state([]);

</script>

<div class="container py-8 space-y-8 max-w-4xl mx-auto">
  <div class="space-y-2">
    <h1 class="text-3xl font-bold tracking-tight">Advanced Select</h1>
    <p class="text-muted-foreground">
      A searchable select component with support for object arrays and
      multi-column table layouts.
    </p>
  </div>

  <Separator />

  <div class="grid gap-8 md:grid-cols-2">
    <!-- Example 1 -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Basic String Array</Card.Title>
        <Card.Description
          >Standard usage with an array of strings.</Card.Description
        >
      </Card.Header>
      <Card.Content class="space-y-4">
        <Select
          options={fruits}
          bind:value={selectedFruit}
          placeholder="Select a fruit"
        />
        <div class="text-sm text-muted-foreground">
          Selected: <span class="font-medium text-foreground"
            >{selectedFruit}</span
          >
        </div>
      </Card.Content>
    </Card.Root>

    <!-- Example 2 -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Object Array</Card.Title>
        <Card.Description
          >Binding to an ID while displaying a name.</Card.Description
        >
      </Card.Header>
      <Card.Content class="space-y-4">
        <Select
          options={users}
          bind:value={selectedUserId}
          valueKey="id"
          labelKey="name"
          placeholder="Select a user"
        />
        <div class="text-sm text-muted-foreground">
          Selected ID: <span class="font-medium text-foreground"
            >{selectedUserId}</span
          >
        </div>
      </Card.Content>
    </Card.Root>
  </div>

  <!-- Example 3 -->
  <Card.Root>
    <Card.Header>
      <Card.Title>Table Layout (Dropdown)</Card.Title>
      <Card.Description>
        A dropdown displaying multiple columns of data for richer context.
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <Select
        options={users}
        bind:value={selectedUserTableId}
        columns={userColumns}
        valueKey="id"
        labelKey="name"
        placeholder="Select a user from table..."
        class="w-full md:w-[400px]"
        contentClass="w-[calc(100vw-2rem)] md:w-[600px]"
      />
      <div class="text-sm text-muted-foreground">
        Selected ID: <span class="font-medium text-foreground"
          >{selectedUserTableId ?? "None"}</span
        >
      </div>
    </Card.Content>
  </Card.Root>

  <!-- Example 4 -->
  <Card.Root>
    <Card.Header>
      <Card.Title>Complex Product Table</Card.Title>
      <Card.Description>
        Advanced table with custom cell formatting, grid columns, and diverse
        data types.
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <Select
        options={products}
        bind:value={selectedProduct}
        columns={productColumns}
        valueKey="id"
        labelKey="name"
        placeholder="Search products..."
        searchPlaceholder="Filter by name, code, category..."
        class="w-full"
        contentClass="w-[calc(100vw-2rem)] md:w-[700px]"
      />
      <div class="rounded-md bg-muted p-4 text-sm font-mono">
        {selectedProduct
          ? JSON.stringify(
              products.find((p) => p.id === selectedProduct),
              null,
              2,
            )
          : "No product selected"}
      </div>
    </Card.Content>
  </Card.Root>

  <!-- Example 5 -->
  <Card.Root>
    <Card.Header>
      <Card.Title>Multiple Selection</Card.Title>
      <Card.Description>Select multiple items from the list.</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <Select
        options={users}
        bind:value={selectedMultipleIds}
        columns={userColumns}
        valueKey="id"
        labelKey="name"
        multiple={true}
        clearable={true}
        placeholder="Select users..."
        class="w-full md:w-[400px]"
        contentClass="w-[calc(100vw-2rem)] md:w-[600px]"
      />
      <div class="text-sm text-muted-foreground">
        Selected IDs: <span class="font-medium text-foreground"
          >{selectedMultipleIds.length > 0
            ? selectedMultipleIds.join(", ")
            : "None"}</span
        >
      </div>
    </Card.Content>
  </Card.Root>
</div>
