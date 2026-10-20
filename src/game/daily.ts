import { QUOTE_COUNT } from "./quotes";

/** today's date, local, as YYYY-MM-DD */
export function todayKey(d = new Date()): string {
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${m}-${day}`;
}

/**
 * The saying of the day: the same for everyone on a given date, and a
 * different one every day for a thousand days before any repeats (389 shares
 * no factor with 1000, so stepping by it visits every page once).
 */
export function dailyIndex(d = new Date()): number {
  const days = Math.floor(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) / 86_400_000);
  return (((days * 389 + 17) % QUOTE_COUNT) + QUOTE_COUNT) % QUOTE_COUNT;
}
