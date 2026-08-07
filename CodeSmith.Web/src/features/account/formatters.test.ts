// == Money and token formatters == //
import { describe, it, expect } from "vitest";
import {
  formatBalanceUsd,
  formatLedgerUsd,
  formatTokenCount,
  freeTokensRemaining,
  ledgerFeatureLabel,
} from "./formatters";

describe("formatBalanceUsd", () => {
  it("renders a true zero as $0.00", () => {
    expect(formatBalanceUsd(0)).toBe("$0.00");
  });

  it("renders a spendable sub-cent balance as < $0.01, never $0.00", () => {
    expect(formatBalanceUsd(0.0042)).toBe("< $0.01");
    expect(formatBalanceUsd(0.0099)).toBe("< $0.01");
  });

  it("renders balances at two decimal places", () => {
    expect(formatBalanceUsd(1)).toBe("$1.00");
    expect(formatBalanceUsd(10.5)).toBe("$10.50");
    expect(formatBalanceUsd(1234.56)).toBe("$1,234.56");
  });
});

describe("formatLedgerUsd", () => {
  it("formats Spend rows at 4 decimal places", () => {
    expect(formatLedgerUsd(0.0042, "Spend")).toBe("$0.0042");
    expect(formatLedgerUsd(1.2, "Spend")).toBe("$1.2000");
  });

  it("formats TopUp rows at 2 decimal places", () => {
    expect(formatLedgerUsd(10, "TopUp")).toBe("$10.00");
    expect(formatLedgerUsd(10.5, "TopUp")).toBe("$10.50");
  });
});

describe("formatTokenCount", () => {
  it("renders thousands-separated token counts", () => {
    expect(formatTokenCount(0)).toBe("0");
    expect(formatTokenCount(20000)).toBe("20,000");
    expect(formatTokenCount(1_234_567)).toBe("1,234,567");
  });
});

describe("freeTokensRemaining", () => {
  it("returns max - used when the grant has headroom", () => {
    expect(freeTokensRemaining(1_200, 20_000)).toBe(18_800);
    expect(freeTokensRemaining(0, 20_000)).toBe(20_000);
  });

  it("never returns negative when freeTokensUsed > freeQuotaMax (reservation hold overshoot)", () => {
    expect(freeTokensRemaining(22_000, 20_000)).toBe(0);
    expect(freeTokensRemaining(20_000, 20_000)).toBe(0);
  });
});

describe("ledgerFeatureLabel", () => {
  it("maps known Feature values to display labels", () => {
    expect(ledgerFeatureLabel("Tutoring:Guidance")).toBe("Paired Programmer · Guidance");
    expect(ledgerFeatureLabel("Billing:TopUp")).toBe("Purchase");
  });

  it("falls back to the raw string for unmapped Feature values rather than blank", () => {
    expect(ledgerFeatureLabel("FutureSurface:NewAction")).toBe("FutureSurface:NewAction");
  });

  it("falls back gracefully when Feature is null or empty", () => {
    expect(ledgerFeatureLabel(null)).toBe("—");
    expect(ledgerFeatureLabel("")).toBe("—");
  });
});
