/**
 * Maps route paths to required permissions.
 * Customize this to match your backend permission names (roleId, name, or values).
 */
export const ROUTE_PERMISSIONS: Record<string, string> = {
  "/dealers": "Dealers",
  "/users": "Users",
  "/respCenters": "Responsibility Centers",
  "/rpt-sales": "RPT-SALES",
  "/gstincheck": "GSTIN Check",
  "/vendors": "Vendors",
  "/sessions": "Sessions",
  "/rungstprocess": "GST Process",
  "/fixed-assets": "Fixed Assets",
  // /assist, /notifications: not listed — any logged-in user may access (no menu permission required)
};

/**
 * Get the required permission for a given path.
 * Uses longest prefix match (e.g. /dealers/ABC123 matches /dealers -> "Dealers").
 */
export function getRequiredPermissionForPath(pathname: string): string {
  const path = (pathname || "/").replace(/\/$/, "") || "/";

  // Exact match first
  if (ROUTE_PERMISSIONS[path]) {
    return ROUTE_PERMISSIONS[path];
  }

  // Prefix match (sorted by length desc so longer paths match first)
  const sorted = Object.entries(ROUTE_PERMISSIONS).sort(
    ([a], [b]) => b.length - a.length,
  );
  for (const [prefix, permission] of sorted) {
    if (path === prefix || path.startsWith(prefix + "/")) {
      return permission;
    }
  }

  return "";
}
