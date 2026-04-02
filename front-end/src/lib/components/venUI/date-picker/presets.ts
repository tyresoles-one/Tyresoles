import { 
    today, 
    getLocalTimeZone, 
    startOfWeek, 
    endOfWeek, 
    startOfMonth, 
    endOfMonth, 
    startOfYear, 
    endOfYear, 
    CalendarDate 
} from "@internationalized/date";
import type { PresetEntry } from "./types";

// Helper to get current date if not provided
const getRef = (date?: CalendarDate) => date ?? today(getLocalTimeZone());

// Single Date Presets
export const single = {
    today: (ref?: CalendarDate): PresetEntry => ({ label: 'Today', value: getRef(ref) }),
    tomorrow: (ref?: CalendarDate): PresetEntry => ({ label: 'Tomorrow', value: getRef(ref).add({ days: 1 }) }),
    yesterday: (ref?: CalendarDate): PresetEntry => ({ label: 'Yesterday', value: getRef(ref).subtract({ days: 1 }) }),
    endOfMonth: (ref?: CalendarDate): PresetEntry => ({ label: 'End of Month', value: endOfMonth(getRef(ref)) }),
    startOfMonth: (ref?: CalendarDate): PresetEntry => ({ label: 'Start of Month', value: startOfMonth(getRef(ref)) }),
};

// Range Presets
export const range = {
    thisWeek: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref);
        return { label: 'This Week', value: { start: startOfWeek(d, 'en-US'), end: endOfWeek(d, 'en-US') } };
    },
    lastWeek: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref).subtract({ weeks: 1 });
        return { label: 'Last Week', value: { start: startOfWeek(d, 'en-US'), end: endOfWeek(d, 'en-US') } };
    },
    thisMonth: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref);
        return { label: 'This Month', value: { start: startOfMonth(d), end: endOfMonth(d) } };
    },
    lastMonth: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref).subtract({ months: 1 });
        return { label: 'Last Month', value: { start: startOfMonth(d), end: endOfMonth(d) } };
    },
    thisYear: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref);
        return { label: 'This Year', value: { start: startOfYear(d), end: endOfYear(d) } };
    },
    lastYear: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref).subtract({ years: 1 });
        return { label: 'Last Year', value: { start: startOfYear(d), end: endOfYear(d) } };
    },
    thisQuarter: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref);
        const startMonth = Math.floor((d.month - 1) / 3) * 3 + 1;
        const start = new CalendarDate(d.year, startMonth, 1);
        return { label: 'This Quarter', value: { start, end: endOfMonth(start.add({ months: 2 })) } };
    },
    lastQuarter: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref).subtract({ months: 3 });
        const startMonth = Math.floor((d.month - 1) / 3) * 3 + 1;
        const start = new CalendarDate(d.year, startMonth, 1);
        return { label: 'Last Quarter', value: { start, end: endOfMonth(start.add({ months: 2 })) } };
    },
    thisFinYear: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref);
        const startYear = d.month >= 4 ? d.year : d.year - 1;
        const start = new CalendarDate(startYear, 4, 1);
        const end = new CalendarDate(startYear + 1, 3, 31);
        return { label: 'This Financial Year', value: { start, end } };
    },
    lastFinYear: (ref?: CalendarDate): PresetEntry => {
        const d = getRef(ref);
        const startYear = d.month >= 4 ? d.year - 1 : d.year - 2;
        const start = new CalendarDate(startYear, 4, 1);
        const end = new CalendarDate(startYear + 1, 3, 31);
        return { label: 'Last Financial Year', value: { start, end } };
    }
};

// Utilities to generate lists
export const getCommonSinglePresets = (ref?: CalendarDate) => [
    single.today(ref),
    single.yesterday(ref),
    single.tomorrow(ref)
];

export const getCommonRangePresets = (ref?: CalendarDate) => [
    range.thisWeek(ref),
    range.lastWeek(ref),
    range.thisMonth(ref),
    range.lastMonth(ref),
    range.thisYear(ref)
];
