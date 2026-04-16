import type { NavEditDisplayColumnFormat } from "./templateTypes";
import { normalizeNavDateToIsoDay } from "./nav-date";

/** Lookup / read-only display for Nav column values (template `displayColumns[].format`). */
export function formatNavDisplayColumnValue(
  raw: string | undefined | null,
  format: NavEditDisplayColumnFormat | undefined
): string {
  if (raw == null || String(raw).trim() === "") return "—";
  const s = String(raw).trim();

  if (format === "date") {
    const iso = normalizeNavDateToIsoDay(s);
    if (!iso) return s;
    const [ys, ms, ds] = iso.split("-");
    const y = Number(ys);
    const m = Number(ms);
    const d = Number(ds);
    if (!ys || Number.isNaN(y) || Number.isNaN(m) || Number.isNaN(d)) return s;
    const dt = new Date(y, m - 1, d);
    if (Number.isNaN(dt.getTime())) return s;
    return dt.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
  }

  if (format === "number") {
    const n = Number(String(s).replace(/,/g, "").replace(/\s/g, ""));
    if (Number.isFinite(n)) return n.toLocaleString("en-IN", { maximumFractionDigits: 8 });
    return s;
  }

  return s;
}

/** Match a KV key to a display column spec (case-insensitive, Nav column names). */
export function formatForDisplayColumnKey(
  key: string,
  specs: { column: string; format?: NavEditDisplayColumnFormat }[]
): NavEditDisplayColumnFormat | undefined {
  const k = key.trim().toLowerCase();
  for (const spec of specs) {
    if (spec.column.trim().toLowerCase() === k) {
      const f = spec.format;
      if (f === "date" || f === "number") return f;
      return undefined;
    }
  }
  return undefined;
}
