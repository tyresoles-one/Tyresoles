# Transition to Single Calling Contact Assignment System

Consolidate the contact assignment system to rely entirely on the newly created `CrmAgentContact` table, removing the legacy `AssignedTo` column on `CrmContact`. This removes redundancy, ensures data integrity, and simplifies the code.

## User Review Required

> [!IMPORTANT]
> **Key Architecture Decisions:**
> 1. **Data Model Consolidation**: The `CrmContact.AssignedTo` property will be removed from the C# entity definitions and EF Core mappings. The actual database column will remain in the table to prevent breaking existing database states, but it will be ignored by the application code.
> 2. **One-Time Startup Migration**: A startup migration SQL script in `Program.cs` will copy any remaining `CrmContact.AssignedTo` values into `CrmAgentContact` (as active allocations) if they are not already synced.
> 3. **UI Form Cleanups**: The "Assigned To" field in the CRM Contacts directory editor (`crm-contacts/+page.svelte`) will be removed since assignments are now managed automatically on visitation or by the supervisor.
> 4. **Query Adaptation**: The CRM Calling workspace page will query `getCrmAgentContacts` (filtering by `agentUsername` and `deallocatedAt == null`) and map the results to the local contact lists.

## Proposed Changes

---

### Backend Components

#### [MODIFY] [CrmContact.cs](file:///d:/Work%20Desk/Tyresoles/back-end/Tyresoles.Data/Features/Crm/Entities/CrmContact.cs)
Remove the `AssignedTo` property definition (line 25).

#### [MODIFY] [CrmContactInput.cs](file:///d:/Work%20Desk/Tyresoles/back-end/Tyresoles.Data/Features/Crm/Models/CrmContactInput.cs)
Remove the `AssignedTo` property definition (line 25).

#### [MODIFY] [CrmDbContext.cs](file:///d:/Work%20Desk/Tyresoles/back-end/Tyresoles.Data/Features/Crm/CrmDbContext.cs)
Remove the property mapping config for `AssignedTo` (line 98).

#### [MODIFY] [Program.cs](file:///d:/Work%20Desk/Tyresoles/back-end/Tyresoles.Web/Program.cs)
Add a SQL script to the startup database initialization block to perform a one-time migration of any legacy assignments in `CrmContact.AssignedTo` into `CrmAgentContact` (only if the active assignment does not already exist in `CrmAgentContact`).

#### [MODIFY] [Mutation.cs](file:///d:/Work%20Desk/Tyresoles/back-end/Tyresoles.Web/Mutation.cs)
- In `SaveCrmContact` (line 214): Remove `contact.AssignedTo = input.AssignedTo;`.
- In `AllocateAgentContacts` (line 937): 
  - Change active allocations count check to query `db.CrmAgentContacts.CountAsync(ac => ac.AgentUsername == callerUserId && ac.DeallocatedAt == null)`.
  - Change unassigned contacts lookup to query `db.CrmContacts.Where(c => c.IsActive && !db.CrmAgentContacts.Any(ac => ac.ContactId == c.Id && ac.DeallocatedAt == null))`.
  - Remove assignment to `contact.AssignedTo`.
- In `DeallocateCrmContact` (line 1037):
  - Find the active allocation from `CrmAgentContacts` and deallocate it.
  - Remove reference to `contact.AssignedTo = null`.

---

### Frontend Components

#### [MODIFY] [crm-contacts page](file:///d:/Work%20Desk/Tyresoles/front-end/src/routes/(user)/(masters)/crm-contacts/+page.svelte)
- Remove `assignedTo` property from Types and `editingContact` state.
- Remove the "Assigned To" `MasterSelect` field from the dialog form (lines 647-653).
- Remove `assignedTo` from GraphQL queries/mutations (`GetCrmContactsDocument` and `SaveCrmContactDocument`).

#### [MODIFY] [crm-calling page](file:///d:/Work%20Desk/Tyresoles/front-end/src/routes/(user)/(masters)/crm-calling/+page.svelte)
- Update query `GetCrmContactsDocument` to be `GetCrmAgentContactsDocument` fetching `crmAgentContacts` items and its nested `contact` fields.
- Update `list` initialization to use `GetCrmAgentContactsDocument` and map to `CrmAgentContact` type.
- Update `filteredContacts` to be derived as `(list.items || []).map(x => x.contact).filter(Boolean)` so that the rest of the page continues to render `CrmContact` items without further code changes.
- Update `buildWhereClause` to structure a nested query filtering by `agentUsername`, `deallocatedAt == null`, and the contact details inside the nested `contact` property.

---

## Verification Plan

### Automated Tests
- Rebuild backend (`dotnet build`) to verify compilation.
- Run frontend codegen (`npm run codegen`) to update typescript types.

### Manual Verification
1. Restart the backend and check logs to verify the database migration ran.
2. Load `/crm-contacts` and verify that the "Assigned To" field is gone and contacts can be saved.
3. Load `/crm-calling` as `tyresoles\navservice` and verify that the 50 contacts load properly.
4. Try logging a call and verify stats update.
5. Try deallocating a contact and verify it disappears.
6. Verify supervisor dashboards report all statistics correctly.
