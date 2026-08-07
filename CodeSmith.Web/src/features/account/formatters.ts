// == Money and token formatters == //

// Balance / pack prices: 2dp. `$0.00` is reserved for a true zero; a spendable sub-cent
// remainder renders `< $0.01` so it never impersonates zero (ticket 008 #11).
export function formatBalanceUsd(amount: number): string {
  if (amount === 0) return "$0.00";
  if (amount > 0 && amount < 0.01) return "< $0.01";
  return formatUsd(amount, 2);
}

// Ledger Spend rows keep 4dp so sub-cent usage stays legible; TopUp rows stay 2dp (ticket 008 #6).
export function formatLedgerUsd(amount: number, type: "Spend" | "TopUp"): string {
  return formatUsd(amount, type === "Spend" ? 4 : 2);
}

export function formatTokenCount(n: number): string {
  return n.toLocaleString("en-US");
}

function formatUsd(amount: number, fractionDigits: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount);
}
