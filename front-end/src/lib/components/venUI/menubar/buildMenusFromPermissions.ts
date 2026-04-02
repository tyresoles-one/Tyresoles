/**
 * Builds menubar MenuItem[] from login response (Menu[]) or fallback for super user.
 * Used by Main/navbar to show navigation from authStore.menus.
 */

import { ROUTE_PERMISSIONS } from "$lib/components/venUI/routeGuard/routePermissions";
import type { MenuItem } from "./types";
import type { Menu } from "$lib/services/graphql/generated/types";

/** Permission name (from ROUTE_PERMISSIONS) -> path (for showAllRoutes fallback) */
const PERMISSION_TO_PATH: Record<string, string> = Object.fromEntries(
  Object.entries(ROUTE_PERMISSIONS).map(([path, name]) => [name, path]),
);

let idCounter = 0;
function slug(s: string): string {
  idCounter++;
  const base = (s || "").trim().replace(/\s+/g, "-").toLowerCase() || "item";
  return `${base}-${idCounter}`;
}

/**
 * Converts login response menus (GraphQL Menu[]) to venUI MenuItem[].
 * Menu → { label, icon, subMenus }; SubMenu → { label, icon, items }; MenuItem (GQL) → { label, icon, action, options }.
 * When a submenu has no label (e.g. "Masters"), that level is skipped and its items become direct children of the parent menu.
 */
export function buildMenusFromLoginMenus(
  menus: Menu[] | null | undefined,
): MenuItem[] {
  const list = menus ?? [];
  if (list.length === 0) return [];

  return list.map((menu, menuIdx) => {
    const id = slug(menu.label) || `menu-${menuIdx}`;
    const children: MenuItem[] = [];

    for (const sub of menu.subMenus ?? []) {
      const subLabel = (sub.label ?? "").trim();
      const subChildren: MenuItem[] = (sub.items ?? []).map(
        (item, itemIdx) => ({
          id: item.action?.trim()
            ? slug(item.action)
            : `item-${menuIdx}-${itemIdx}`,
          label: item.label ?? "",
          icon: item.icon ?? undefined,
          href: item.action?.trim().startsWith("/")
            ? item.action.trim()
            : undefined,
          options: item.options?.trim() ? item.options : undefined,
        }),
      );

      // Skip one level if submenu label is missing: promote items directly under the parent (e.g. Masters)
      if (!subLabel) {
        children.push(...subChildren);
        continue;
      }

      const subId = slug(sub.label) || `sub-${children.length}`;
      if (subChildren.length === 1 && !subChildren[0].children?.length) {
        children.push({
          id: subChildren[0].id,
          label: subChildren[0].label,
          icon: subChildren[0].icon,
          href: subChildren[0].href,
          options: subChildren[0].options,
        });
      } else {
        children.push({
          id: subId,
          label: sub.label ?? "",
          icon: sub.icon ?? undefined,
          children: subChildren,
        });
      }
    }

    return {
      id,
      label: menu.label ?? "",
      icon: menu.icon ?? undefined,
      children: children.length > 0 ? children : undefined,
    };
  });
}

/**
 * Returns all route paths for "show all routes" (e.g. super user when no menus).
 */
function buildShowAllRoutesMenu(): MenuItem[] {
  const navChildren: MenuItem[] = Object.entries(PERMISSION_TO_PATH).map(
    ([label, path]) => ({
      id: path.slice(1) || "home",
      label,
      href: path,
    }),
  );
  return [
    {
      id: "nav",
      label: "Navigation",
      icon: "Compass",
      children: navChildren,
    },
  ];
}

/**
 * Returns menu items for the navbar from authStore.menus (login response).
 * When menus are present, converts them via buildMenusFromLoginMenus.
 * When menus are empty and userType is 'super', shows all routes.
 */
export function buildMenusFromUser(
  menus: Menu[] | null | undefined,
  userType?: string | null,
): MenuItem[] {
  let list = menus ?? [];
  let results: MenuItem[] = [];

  if (list.length > 0) {
    results = buildMenusFromLoginMenus(list);
  } else if ((userType || "").toLowerCase() === "super") {
    results = buildShowAllRoutesMenu();
  }

  return results;
}
