# Tasks - Transition to Single Assignment System

- [x] Backend Modifications (C# / EF Core)
  - [x] Remove `AssignedTo` property from `CrmContact.cs`
  - [x] Remove `AssignedTo` property from `CrmContactInput.cs`
  - [x] Update `CrmDbContext.cs` entity configuration mapping
  - [x] Add legacy assignment migration script to `Program.cs`
  - [x] Update mutations in `Mutation.cs`:
    - [x] Update `SaveCrmContact`
    - [x] Update `AllocateAgentContacts`
    - [x] Update `DeallocateCrmContact`
  - [x] Build backend using `dotnet build`
- [ ] Frontend Modifications (Svelte / TypeScript)
  - [ ] Run GraphQL Codegen (`npm run codegen`) to update types
  - [ ] Update `crm-contacts/+page.svelte` (remove Assigned To field and queries)
  - [ ] Update `crm-calling/+page.svelte` (query `crmAgentContacts` and map items)
- [ ] Verification
  - [ ] Rebuild and run application
  - [ ] Validate contacts and calling page functionality manually
