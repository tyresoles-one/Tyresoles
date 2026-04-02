# Event Calendar Module – Activation Guide (Front-End & Back-End)

This document explains how the **event calendar module** is activated and how to make it visible in the app (including in the navigation menu).

---

## Summary

- **Back-end**: The calendar module is **already active**. It uses a separate SQL Server database, EF Core, and GraphQL. No feature flag needs to be turned on.
- **Front-end**: The calendar **page and API usage are already active**. The route `/calendar` is available to any logged-in user. The only optional step is making the **Calendar** link appear in the **navigation menu**, which is driven by backend **Permission Set** data from the NAV/Dataverse database.

---

## 1. Back-End Activation

### 1.1 What is already active

The event calendar is wired in the ASP.NET Core backend as follows.

| Component | Location | Purpose |
|-----------|----------|---------|
| **Calendar DbContext** | `Tyresoles.Data.Features.Calendar.CalendarDbContext` | EF Core context for calendar DB (events, types, reminders, shares, etc.) |
| **Calendar service** | `Tyresoles.Data.Features.Calendar.CalendarService` (implements `ICalendarService`) | Business logic: CRUD events, event types, reminders, sharing |
| **DI registration** | `Program.cs` | `AddDbContext<CalendarDbContext>` and `AddScoped<ICalendarService, CalendarService>` |
| **DB creation & seed** | `Program.cs` (startup) | `EnsureCreatedAsync()` and seed of default **Event Types** (Meeting, Call, Visit, Task, Leave) |
| **GraphQL Query** | `Tyresoles.Web.Query.cs` | `GetMyCalendarEvents`, `GetCalendarEventById`, `GetEventTypes`, `GetUpcomingReminders`, `GetSharedCalendars`, `GetNotificationPreference` |
| **GraphQL Mutation** | `Tyresoles.Web.Mutation.cs` | `CreateCalendarEvent`, `UpdateCalendarEvent`, `DeleteCalendarEvent`, `SnoozeReminder`, `ShareCalendar`, etc. |

So the **calendar API is already active**; no extra “enable” switch is required in code.

### 1.2 Configuration

- **Connection string**  
  Calendar uses its own database. In `appsettings.json`:

  ```json
  "ConnectionStrings": {
    "Calendar": "Server=(localdb)\\mssqllocaldb;Database=TyresolesCalendar;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
  ```

  If omitted, the code falls back to the same default (see `Program.cs`). For production, set `ConnectionStrings:Calendar` to your calendar DB server/database.

- **Authentication**  
  All calendar GraphQL operations use `[Authorize]` and resolve the current user from JWT (e.g. `ClaimTypes.NameIdentifier` or `"sub"`). No extra auth config is needed beyond your existing JWT setup.

### 1.3 What you need to do on the back-end

1. **Ensure the calendar database exists**  
   On first run, `Program.cs` calls `EnsureCreatedAsync()` for `CalendarDbContext`, so the database is created and default event types are seeded. Just run the app once with a valid Calendar connection string.

2. **Optional: Show “Calendar” in the user’s navigation menu**  
   The **menu items** (navbar / menu page) come from the **login response**: the backend builds `menus` from **Permission Set** and **Access Control** in the **NAV/Dataverse** database (see `UserService.BuildMenusFromPermissions` and `LoadUserGraphRawAsync`).  
   To show a **Calendar** entry in the menu:

   - In the **Permission Set** table (NAV/Business Central), ensure there is a row that will be joined to the user’s role(s) (via **Access Control**), with for example:
     - **Action**: `"/calendar"`
     - **Name**: `"Calendar"` (or the label you want)
     - **Web App**: `1` (so it appears for web)
     - **Parent Menu** / **Sub Menu** / **Order** / **Icon** as desired.
   - Assign that **Role ID** to the relevant users (through your existing Access Control / role setup).

   If you don’t add this, the calendar **page and API still work**; only the **menu link** will be missing (users can still open `/calendar` directly if they know the URL, and super users may see it under “show all routes” depending on your front-end logic).

---

## 2. Front-End Activation

### 2.1 What is already active

| Component | Location | Purpose |
|-----------|----------|---------|
| **Route** | `src/routes/(user)/calendar/+page.svelte` | Calendar page at **`/calendar`** |
| **Calendar API client** | `src/lib/services/calendar/api.ts` | GraphQL calls: `getMyCalendarEvents`, `getEventTypes`, `createCalendarEvent`, `updateCalendarEvent`, `deleteCalendarEvent`, `getUpcomingReminders`, `snoozeReminder`, etc. |
| **UI component** | `src/lib/components/venUI/calendar-view/CalendarView.svelte` | Month grid, events per day, create/edit/delete |
| **Route permission** | `src/lib/components/venUI/routeGuard/routePermissions.ts` | `"/calendar": ""` → **empty** required permission, so any **logged-in** user is allowed |

So the **calendar page and all calendar API usage are already active** for any authenticated user.

### 2.2 How access is enforced

- **Layout**  
  The `(user)` layout uses `RouteGuard` with `requiredPermission = getRequiredPermissionForPath($page.url.pathname)`.

- **Permission for `/calendar`**  
  In `routePermissions.ts`, `"/calendar"` is mapped to `""`. In `RouteGuard.svelte`, when `requiredPermission` is empty, the guard treats the route as **allowed** (no permission check). So:

  - User must be **logged in** (otherwise redirected to login).
  - No extra permission is required for `/calendar`; **any authenticated user can open the calendar**.

### 2.3 Showing “Calendar” in the navigation menu

The **navbar / menu** is built from **login response menus** (see `buildMenusFromUser` / `buildMenusFromLoginMenus` in `menubar/buildMenusFromPermissions.ts`). Each menu item comes from the backend:

- **Backend** returns `Menu` → `SubMenus` → `Items`; each item has **Action** (e.g. `"/calendar"`) and **Label**.
- **Front-end** converts that into navbar/menu entries; if an item has `action === "/calendar"`, it will show as a link to the calendar.

So:

- To show **Calendar** in the menu, the **backend** must include a menu item with **Action** `"/calendar"` in the login `menus` (see §1.3: Permission Set in NAV).
- No extra front-end “activation” is needed beyond that; once the backend sends the item, the link will appear.

### 2.4 Optional: Label for “show all routes” (e.g. super user)

When the user has **no menus** and is treated as super user, the front-end can build a “show all routes” menu from `ROUTE_PERMISSIONS`. For `/calendar` the value is currently `""`, so the label can be empty in that fallback list. If you want a clear label (e.g. “Calendar”) in that case, set:

```ts
// routePermissions.ts
"/calendar": "Calendar",
```

This does not change who can access the page; it only affects the displayed name when the route is shown in the “all routes” menu.

---

## 3. End-to-End Checklist

| Step | Where | Action |
|------|--------|--------|
| 1 | Back-end | Use a valid `ConnectionStrings:Calendar` (or default). Run the app once so calendar DB is created and seeded. |
| 2 | Back-end | No code change needed to “enable” calendar API; it’s already registered and exposed via GraphQL. |
| 3 | Front-end | No code change needed to “enable” the calendar page or API client; route and services are already in use. |
| 4 | NAV / Dataverse | To show **Calendar** in the app menu: add (or ensure there is) a **Permission Set** row with **Action** `"/calendar"`, **Web App** = 1, and the right **Role ID**, and assign that role to users. |
| 5 | Front-end (optional) | Set `"/calendar": "Calendar"` in `routePermissions.ts` if you want a proper label in the “show all routes” menu. |

---

## 4. Quick Reference – Key Files

**Back-end**

- `Tyresoles.Web/Program.cs` – Calendar DbContext and service registration; DB ensure created and event type seed.
- `Tyresoles.Web/Query.cs` – Calendar GraphQL queries.
- `Tyresoles.Web/Mutation.cs` – Calendar GraphQL mutations.
- `Tyresoles.Data/Features/Calendar/` – Calendar entities, DbContext, DTOs, and `CalendarService`.
- `Tyresoles.Data/Features/Admin/User/UserService.cs` – Builds login `menus` from Permission Set + Access Control (NAV).

**Front-end**

- `src/routes/(user)/calendar/+page.svelte` – Calendar page.
- `src/lib/services/calendar/api.ts` – Calendar GraphQL client.
- `src/lib/components/venUI/calendar-view/` – Calendar UI.
- `src/lib/components/venUI/routeGuard/routePermissions.ts` – Path → permission (e.g. `"/calendar": ""`).
- `src/lib/components/venUI/menubar/buildMenusFromPermissions.ts` – Builds navbar/menu from login `menus`.

---

With the above, the event calendar module is **active** on both front-end and back-end; the only optional step is configuring the **Permission Set** in the NAV/Dataverse DB so the **Calendar** link appears in the navigation menu for the right users.
