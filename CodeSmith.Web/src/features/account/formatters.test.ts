// == Money and token formatters == //
import { describe, it, expect } from "vitest";
import { formatBalanceUsd, formatLedgerUsd, formatTokenCount } from "./formatters";

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
