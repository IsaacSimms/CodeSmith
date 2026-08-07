// == Post-checkout baseline: TopUp fingerprints in sessionStorage == //
// Snapshot at checkout start so a webhook that lands before return is still detected.
import type { LedgerEntryResponse } from "./types";

export const CHECKOUT_BASELINE_KEY = "codesmith.checkoutBaseline";

export interface TopUpFingerprint {
  timestampUtc: string;
  amountUsd: number;
}

export interface CheckoutBaseline {
  fingerprints: TopUpFingerprint[];
  pending: boolean; // true while post-checkout poll / banner may still run
}

// == Extract TopUp fingerprints (ledger DTO has no row id) == //
export function topUpFingerprints(entries: LedgerEntryResponse[]): TopUpFingerprint[] {
  return entries
    .filter((e) => e.type === "TopUp")
    .map((e) => ({ timestampUtc: e.timestampUtc, amountUsd: e.amountUsd }));
}

function fingerprintKey(fp: TopUpFingerprint): string {
  return `${fp.timestampUtc}|${fp.amountUsd}`;
}

// == sessionStorage read/write == //
export function writeCheckoutBaseline(fingerprints: TopUpFingerprint[]): void {
  const payload: CheckoutBaseline = { fingerprints, pending: true };
  sessionStorage.setItem(CHECKOUT_BASELINE_KEY, JSON.stringify(payload));
}

export function readCheckoutBaseline(): CheckoutBaseline | null {
  const raw = sessionStorage.getItem(CHECKOUT_BASELINE_KEY);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as CheckoutBaseline;
    if (!parsed || !Array.isArray(parsed.fingerprints)) return null;
    return {
      fingerprints: parsed.fingerprints,
      pending: parsed.pending === true,
    };
  } catch {
    return null;
  }
}

export function clearCheckoutBaseline(): void {
  sessionStorage.removeItem(CHECKOUT_BASELINE_KEY);
}

export function isCheckoutPending(): boolean {
  return readCheckoutBaseline()?.pending === true;
}

// First TopUp whose fingerprint is not in the pre-checkout baseline.
export function findNewTopUp(
  entries: LedgerEntryResponse[],
  baseline: TopUpFingerprint[]
): LedgerEntryResponse | null {
  const known = new Set(baseline.map(fingerprintKey));
  for (const entry of entries) {
    if (entry.type !== "TopUp") continue;
    const key = fingerprintKey({
      timestampUtc: entry.timestampUtc,
      amountUsd: entry.amountUsd,
    });
    if (!known.has(key)) return entry;
  }
  return null;
}
