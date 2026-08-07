// == Checkout baseline: TopUp fingerprints in sessionStorage == //
import { describe, it, expect, beforeEach } from "vitest";
import type { LedgerEntryResponse } from "./types";
import {
  CHECKOUT_BASELINE_KEY,
  clearCheckoutBaseline,
  findNewTopUp,
  isCheckoutPending,
  readCheckoutBaseline,
  topUpFingerprints,
  writeCheckoutBaseline,
} from "./checkoutBaseline";

const topUp = (
  amountUsd: number,
  timestampUtc: string
): LedgerEntryResponse => ({
  type: "TopUp",
  amountUsd,
  isFreeCovered: false,
  feature: "Billing:TopUp",
  timestampUtc,
});

const spend = (amountUsd: number, timestampUtc: string): LedgerEntryResponse => ({
  type: "Spend",
  amountUsd,
  isFreeCovered: false,
  feature: "Tutoring:Guidance",
  timestampUtc,
});

beforeEach(() => {
  sessionStorage.clear();
});

describe("topUpFingerprints", () => {
  it("keeps only TopUp rows as (timestampUtc, amountUsd) pairs", () => {
    const entries: LedgerEntryResponse[] = [
      spend(0.01, "2026-08-01T10:00:00Z"),
      topUp(10, "2026-08-01T09:00:00Z"),
      topUp(5, "2026-08-01T08:00:00Z"),
    ];

    expect(topUpFingerprints(entries)).toEqual([
      { timestampUtc: "2026-08-01T09:00:00Z", amountUsd: 10 },
      { timestampUtc: "2026-08-01T08:00:00Z", amountUsd: 5 },
    ]);
  });
});

describe("writeCheckoutBaseline / readCheckoutBaseline", () => {
  it("persists fingerprints and pending intent under the stable key", () => {
    const fps = [{ timestampUtc: "2026-08-01T09:00:00Z", amountUsd: 10 }];
    writeCheckoutBaseline(fps);

    expect(sessionStorage.getItem(CHECKOUT_BASELINE_KEY)).not.toBeNull();
    expect(readCheckoutBaseline()).toEqual({ fingerprints: fps, pending: true });
    expect(isCheckoutPending()).toBe(true);
  });

  it("clearCheckoutBaseline removes storage so pending is false", () => {
    writeCheckoutBaseline([]);
    clearCheckoutBaseline();

    expect(readCheckoutBaseline()).toBeNull();
    expect(isCheckoutPending()).toBe(false);
    expect(sessionStorage.getItem(CHECKOUT_BASELINE_KEY)).toBeNull();
  });
});

describe("findNewTopUp", () => {
  it("returns the first TopUp absent from the baseline fingerprints", () => {
    const baseline = [{ timestampUtc: "2026-08-01T08:00:00Z", amountUsd: 5 }];
    const entries = [
      topUp(10, "2026-08-01T09:00:00Z"),
      topUp(5, "2026-08-01T08:00:00Z"),
      spend(0.02, "2026-08-01T09:01:00Z"),
    ];

    expect(findNewTopUp(entries, baseline)).toEqual(entries[0]);
  });

  it("returns null when every TopUp is already in the baseline", () => {
    const baseline = [
      { timestampUtc: "2026-08-01T09:00:00Z", amountUsd: 10 },
      { timestampUtc: "2026-08-01T08:00:00Z", amountUsd: 5 },
    ];
    const entries = [topUp(10, "2026-08-01T09:00:00Z"), topUp(5, "2026-08-01T08:00:00Z")];

    expect(findNewTopUp(entries, baseline)).toBeNull();
  });
});
