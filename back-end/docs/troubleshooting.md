# Troubleshooting

Common issues and remediation strategies surrounding Tyresoles.Sql operations across specific environments natively.

## 1. "Source generator not working"
Usually occurring when editing code rapidly or the IDE caching prevents evaluation. Keep in mind generators trigger directly on Compile, not immediately visually!

**Solution**:
1. Execute a heavy clean process over your assembly via CLI:
```bash
dotnet clean && dotnet build
```
2. Ensure you have targeted `NET SDK 10` internally across referencing configurations (`<LangVersion>13.0</LangVersion>`).
3. Ensure the generator emits natively by modifying `.csproj`:
```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
```
Inspect `obj/Debug/net10.0/generated` dynamically on your file system directly.

## 2. Invalid NAV Filter Parsing Exception
Occurs when trying to run `.Filter("string")` using an invalid classic NAV parameter structure (Like omitting `FILTER()`).

**Solution**:
Ensure syntax respects NAV's strict regex mappings.
- **Fail:** `.Filter("BLOCKED=<>1")`
- **Pass:** `.Filter("BLOCKED=FILTER(<>1)")`

## 3. High Memory Processing during Batch Scripts
Typically invoked when wrapping arrays inside huge `.ToArrayAsync()` mapping sequences entirely loading the list simultaneously directly into the Heap memory allocation spaces. 

**Solution**:
```csharp
// Convert explicitly into Stream operations exclusively traversing readers
await foreach(var p in scope.StreamAsync(query)) 
{ 
     // Handle instance singularly 
}
```

## 4. GraphQL Connection Type Binding Error 
Arises heavily if HotChocolate fails executing queries dynamically due to missing `IQueryable` endpoints natively matching your defined schema exactly. Verify `Tyresoles.Sql.Generators` emits extensions precisely matching your root endpoints and the `[NavTable]` exists cleanly on all requested nested tables mapped out of the AST closure.
